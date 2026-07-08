using StockControl.Application.Recetas;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Tests;

public class RecetaServiceTests
{
    [Fact]
    public async Task Costo_UsaPrecioPonderadoReciente()
    {
        using var db = TestDb.Crear();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        // Compras recientes de tomate: (10×6 + 30×7) / 40 = Q6.75/lb ponderado.
        TestDb.AgregarCompra(db, 1, "R-01", hoy.AddDays(-5), 10, 6);
        TestDb.AgregarCompra(db, 1, "R-02", hoy.AddDays(-3), 30, 7);

        var service = new RecetaService(db);
        var plato = await service.CrearAsync(new CrearPlatoRequest("Ensalada", 30));
        var conIngrediente = await service.UpsertIngredienteAsync(
            plato.Id, new UpsertIngredienteRequest(1, 2)); // 2 lb de tomate por porción

        Assert.NotNull(conIngrediente);
        Assert.Equal(13.5m, conIngrediente!.Costo);           // 2 × 6.75
        Assert.Equal(16.5m, conIngrediente.Margen);           // 30 − 13.5
        Assert.Equal(45m, conIngrediente.FoodCostPorcentaje); // 13.5 / 30
        Assert.True(conIngrediente.CostoCompleto);
    }

    [Fact]
    public async Task Costo_SinComprasRecientes_CaeAlUltimoPrecioHistorico()
    {
        using var db = TestDb.Crear();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        // Única compra hace 90 días a Q5: fuera de la ventana de 30 días.
        TestDb.AgregarCompra(db, 1, "R-03", hoy.AddDays(-90), 10, 5);

        var service = new RecetaService(db);
        var plato = await service.CrearAsync(new CrearPlatoRequest("Sopa", null));
        var conIngrediente = await service.UpsertIngredienteAsync(plato.Id, new UpsertIngredienteRequest(1, 3));

        Assert.Equal(15m, conIngrediente!.Costo); // 3 × 5 (último histórico)
        Assert.True(conIngrediente.CostoCompleto);
        Assert.Null(conIngrediente.Margen); // sin precio de venta
    }

    [Fact]
    public async Task Producto_SinCompraJamas_MarcaCostoIncompleto()
    {
        using var db = TestDb.Crear();
        var service = new RecetaService(db);

        var plato = await service.CrearAsync(new CrearPlatoRequest("Nuevo", 20));
        var conIngrediente = await service.UpsertIngredienteAsync(plato.Id, new UpsertIngredienteRequest(1, 2));

        Assert.Equal(0m, conIngrediente!.Costo);
        Assert.False(conIngrediente.CostoCompleto);
    }

    [Fact]
    public async Task UpsertIngrediente_MismoProducto_ActualizaEnVezDeDuplicar()
    {
        using var db = TestDb.Crear();
        var service = new RecetaService(db);
        var plato = await service.CrearAsync(new CrearPlatoRequest("Plato", null));

        await service.UpsertIngredienteAsync(plato.Id, new UpsertIngredienteRequest(1, 2));
        var actualizado = await service.UpsertIngredienteAsync(plato.Id, new UpsertIngredienteRequest(1, 5));

        var ingrediente = Assert.Single(actualizado!.Ingredientes);
        Assert.Equal(5m, ingrediente.CantidadPorPorcion);
    }

    [Fact]
    public async Task Impacto_CalculaPesoDelProductoEnCadaPlato()
    {
        using var db = TestDb.Crear();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        TestDb.AgregarCompra(db, 1, "R-04", hoy.AddDays(-5), 100, 6); // tomate Q6/lb

        // Segundo producto para que el plato tenga más de un ingrediente.
        db.Productos.Add(new Producto { Id = 2, Nombre = "Cebolla", Categoria = CategoriaProducto.Verdura, UnidadBaseId = 1 });
        db.Conversiones.Add(new ConversionProducto { ProductoId = 2, UnidadId = 1, FactorABase = 1m });
        db.SaveChanges();
        var doc = new DocumentoCompra
        {
            Fecha = hoy.AddDays(-4), NumeroDocumento = "R-05", HotelId = 1, ProveedorId = 1,
            Detalles = { new DetalleCompra { ProductoId = 2, UnidadId = 1, Cantidad = 10, PrecioUnitario = 4, FactorABase = 1 } },
        };
        db.Documentos.Add(doc);
        db.SaveChanges();

        var service = new RecetaService(db);
        var plato = await service.CrearAsync(new CrearPlatoRequest("Salsa", 50));
        await service.UpsertIngredienteAsync(plato.Id, new UpsertIngredienteRequest(1, 2)); // tomate: 2×6 = 12
        await service.UpsertIngredienteAsync(plato.Id, new UpsertIngredienteRequest(2, 1)); // cebolla: 1×4 = 4

        var impacto = await service.ImpactoProductoAsync(1); // tomate

        var fila = Assert.Single(impacto);
        Assert.Equal("Salsa", fila.Plato);
        Assert.Equal(12m, fila.CostoLinea);
        Assert.Equal(16m, fila.CostoPlato);
        Assert.Equal(75m, fila.PorcentajeDelCosto); // 12 / 16
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task CantidadPorPorcionInvalida_Falla(double cantidad)
    {
        using var db = TestDb.Crear();
        var service = new RecetaService(db);
        var plato = await service.CrearAsync(new CrearPlatoRequest("Plato", null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpsertIngredienteAsync(plato.Id, new UpsertIngredienteRequest(1, (decimal)cantidad)));
    }
}
