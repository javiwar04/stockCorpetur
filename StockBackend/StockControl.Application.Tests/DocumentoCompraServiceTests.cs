using StockControl.Application.Compras;
using StockControl.Application.Inventario;
using StockControl.Domain.Enums;

namespace StockControl.Application.Tests;

public class DocumentoCompraServiceTests
{
    private static CrearDocumentoCompraRequest Peticion(int hotelId = 1, string numero = "DOC-001") => new(
        new DateOnly(2026, 7, 1), numero, hotelId, 1, 0, null,
        [new CrearDetalleCompraRequest(1, 1, 10, 6)]);

    [Fact]
    public async Task Crear_CalculaTotalYGuarda()
    {
        using var db = TestDb.Crear();
        var service = new DocumentoCompraService(db, new CurrentUserFake(esAdmin: true));

        var doc = await service.CrearAsync(Peticion());

        Assert.Equal(60m, doc.Total);
        Assert.Equal(EstadoDocumentoCompra.Recibido.ToString(), doc.Estado);
        Assert.Single(doc.Detalles);
    }

    [Fact]
    public async Task Crear_Borrador_NoCuentaEnInventarioHastaRecibir()
    {
        using var db = TestDb.Crear();
        var service = new DocumentoCompraService(db, new CurrentUserFake(esAdmin: true));
        var inventario = new InventarioService(db, new CurrentUserFake(esAdmin: true));
        var peticion = Peticion() with { Estado = EstadoDocumentoCompra.Borrador.ToString() };

        var doc = await service.CrearAsync(peticion);

        Assert.Equal(EstadoDocumentoCompra.Borrador.ToString(), doc.Estado);
        Assert.Empty(await inventario.ExistenciasAsync(1));

        var recibido = await service.RecibirAsync(doc.Id);

        Assert.NotNull(recibido);
        Assert.Equal(EstadoDocumentoCompra.Recibido.ToString(), recibido!.Estado);
        Assert.Equal(10m, Assert.Single(await inventario.ExistenciasAsync(1)).Existencia);
    }

    [Fact]
    public async Task Anular_ExcluyeDocumentoDelInventario()
    {
        using var db = TestDb.Crear();
        var service = new DocumentoCompraService(db, new CurrentUserFake(esAdmin: true));
        var inventario = new InventarioService(db, new CurrentUserFake(esAdmin: true));
        var doc = await service.CrearAsync(Peticion());

        Assert.Equal(10m, Assert.Single(await inventario.ExistenciasAsync(1)).Existencia);

        var anulado = await service.AnularAsync(doc.Id);

        Assert.NotNull(anulado);
        Assert.Equal(EstadoDocumentoCompra.Anulado.ToString(), anulado!.Estado);
        Assert.Empty(await inventario.ExistenciasAsync(1));
    }

    [Fact]
    public async Task Crear_NumeroDuplicadoEnMismoHotel_Falla()
    {
        using var db = TestDb.Crear();
        var service = new DocumentoCompraService(db, new CurrentUserFake(esAdmin: true));
        await service.CrearAsync(Peticion());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CrearAsync(Peticion()));
    }

    [Fact]
    public async Task Crear_MismoNumeroEnOtroHotel_EsValido()
    {
        using var db = TestDb.Crear();
        var service = new DocumentoCompraService(db, new CurrentUserFake(esAdmin: true));
        await service.CrearAsync(Peticion(hotelId: 1));

        var doc = await service.CrearAsync(Peticion(hotelId: 2));
        Assert.Equal(2, doc.HotelId);
    }

    [Fact]
    public async Task Digitador_NoPuedeCrearEnHotelAjeno()
    {
        using var db = TestDb.Crear();
        var service = new DocumentoCompraService(db, new CurrentUserFake(hoteles: 2));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CrearAsync(Peticion(hotelId: 1)));
    }

    [Fact]
    public async Task Digitador_SoloListaSusHoteles()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "H1-001", new DateOnly(2026, 7, 1), 10, 6);
        TestDb.AgregarCompra(db, 2, "H2-001", new DateOnly(2026, 7, 1), 5, 6);

        var service = new DocumentoCompraService(db, new CurrentUserFake(hoteles: 2));
        var docs = await service.ListarAsync(new FiltroDocumentos(null, null, null, null));

        Assert.Single(docs);
        Assert.Equal("H2-001", docs[0].NumeroDocumento);
    }

    [Fact]
    public async Task Crear_SinConversionConfigurada_Falla()
    {
        using var db = TestDb.Crear();
        var service = new DocumentoCompraService(db, new CurrentUserFake(esAdmin: true));
        // Unidad 99 no existe como conversión del producto 1.
        var peticion = new CrearDocumentoCompraRequest(
            new DateOnly(2026, 7, 1), "DOC-X", 1, 1, 0, null,
            [new CrearDetalleCompraRequest(1, 99, 10, 6)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CrearAsync(peticion));
    }

    [Fact]
    public async Task Crear_CopiaElFactorDeConversionDelMomento()
    {
        using var db = TestDb.Crear();
        var service = new DocumentoCompraService(db, new CurrentUserFake(esAdmin: true));
        // Compra en cajas (unidad 2, factor 25).
        var peticion = new CrearDocumentoCompraRequest(
            new DateOnly(2026, 7, 1), "DOC-CAJA", 1, 1, 0, null,
            [new CrearDetalleCompraRequest(1, 2, 2, 150)]);

        await service.CrearAsync(peticion);

        var detalle = db.Detalles.Single(d => d.DocumentoCompra.NumeroDocumento == "DOC-CAJA");
        Assert.Equal(25m, detalle.FactorABase);
        Assert.Equal(50m, detalle.CantidadBase);
        Assert.Equal(6m, detalle.PrecioPorUnidadBase);
    }

    [Fact]
    public async Task Actualizar_ReemplazaLineasYRecalcula()
    {
        using var db = TestDb.Crear();
        var service = new DocumentoCompraService(db, new CurrentUserFake(esAdmin: true));
        var creado = await service.CrearAsync(Peticion());

        var actualizado = await service.ActualizarAsync(creado.Id, new CrearDocumentoCompraRequest(
            new DateOnly(2026, 7, 2), "DOC-001", 1, 1, 0, "corregido",
            [new CrearDetalleCompraRequest(1, 1, 8, 6)]));

        Assert.NotNull(actualizado);
        Assert.Equal(48m, actualizado!.Total);
        Assert.Single(actualizado.Detalles);
        Assert.Equal(1, db.Detalles.Count()); // las líneas viejas no quedan huérfanas
    }

    [Fact]
    public async Task Actualizar_DocumentoAnulado_Falla()
    {
        using var db = TestDb.Crear();
        var service = new DocumentoCompraService(db, new CurrentUserFake(esAdmin: true));
        var creado = await service.CrearAsync(Peticion());
        await service.AnularAsync(creado.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ActualizarAsync(creado.Id, Peticion()));
    }

    [Fact]
    public async Task Eliminar_DigitadorHotelAjeno_Falla()
    {
        using var db = TestDb.Crear();
        var doc = TestDb.AgregarCompra(db, 1, "H1-DEL", new DateOnly(2026, 7, 1), 10, 6);

        var service = new DocumentoCompraService(db, new CurrentUserFake(hoteles: 2));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EliminarAsync(doc.Id));
    }
}
