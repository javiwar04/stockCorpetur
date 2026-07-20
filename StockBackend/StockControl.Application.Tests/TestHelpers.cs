using Microsoft.EntityFrameworkCore;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;
using StockControl.Infrastructure.Persistence;

namespace StockControl.Application.Tests;

/// <summary>Usuario falso para probar el scoping sin JWT real.</summary>
public class CurrentUserFake(bool esAdmin = false, bool esGerencia = false, params int[] hoteles) : ICurrentUser
{
    public string? UserId => "test-user";
    public string? UserName => "tester";
    public bool EstaAutenticado => true;
    public bool EsAdmin => esAdmin;
    public bool EsGerencia => esGerencia;
    public IReadOnlyCollection<int> HotelesPermitidos => hoteles;

    public bool PuedeAccederHotel(int hotelId) =>
        EsAdmin || EsGerencia || HotelesPermitidos.Contains(hotelId);
}

public static class TestDb
{
    /// <summary>Contexto InMemory aislado por test, con catálogos mínimos sembrados.</summary>
    public static AppDbContext Crear()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);

        db.Hoteles.AddRange(
            new Hotel { Id = 1, Nombre = "Hotel Uno" },
            new Hotel { Id = 2, Nombre = "Hotel Dos" });
        db.Proveedores.Add(new Proveedor { Id = 1, Nombre = "Proveedor Test" });
        db.Unidades.AddRange(
            new UnidadMedida { Id = 1, Nombre = "Libra", Abreviatura = "lb" },
            new UnidadMedida { Id = 2, Nombre = "Caja", Abreviatura = "cja" });
        db.Productos.Add(new Producto { Id = 1, Nombre = "Tomate", Categoria = CategoriaProducto.Verdura, UnidadBaseId = 1 });
        db.Conversiones.AddRange(
            new ConversionProducto { Id = 1, ProductoId = 1, UnidadId = 1, FactorABase = 1m },
            new ConversionProducto { Id = 2, ProductoId = 1, UnidadId = 2, FactorABase = 25m });
        db.SaveChanges();

        return db;
    }

    /// <summary>Inserta un documento con una línea de tomate en libras.</summary>
    public static DocumentoCompra AgregarCompra(
        AppDbContext db, int hotelId, string numero, DateOnly fecha, decimal cantidad, decimal precio)
    {
        var doc = new DocumentoCompra
        {
            Fecha = fecha,
            NumeroDocumento = numero,
            NumeroPedido = numero,
            HotelId = hotelId,
            ProveedorId = 1,
            Detalles =
            {
                new DetalleCompra { ProductoId = 1, UnidadId = 1, Cantidad = cantidad, PrecioUnitario = precio, FactorABase = 1m },
            },
        };
        db.Documentos.Add(doc);
        db.SaveChanges();
        return doc;
    }

    public static CierreMensual AgregarCierre(AppDbContext db, int hotelId, int anio, int mes)
    {
        var cierre = new CierreMensual
        {
            HotelId = hotelId,
            Anio = anio,
            Mes = mes,
            Estado = EstadoCierreMensual.Cerrado,
            FechaCierre = DateTime.UtcNow,
        };
        db.CierresMensuales.Add(cierre);
        db.SaveChanges();
        return cierre;
    }
}
