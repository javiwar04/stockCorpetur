using StockControl.Application.Alertas;
using StockControl.Domain.Entities;

namespace StockControl.Application.Tests;

public class AlertaServiceTests
{
    [Fact]
    public async Task Listar_AdminIncluyeStockCuentasYCierresPendientes()
    {
        using var db = TestDb.Crear();
        db.Proveedores.Single(p => p.Id == 1).DiasCredito = 0;
        db.StockMinimos.Add(new StockMinimo { HotelId = 1, ProductoId = 1, CantidadMinimaBase = 20 });
        TestDb.AgregarCompra(db, 1, "ALT-CXP", new DateOnly(2000, 1, 1), 10, 5);
        await db.SaveChangesAsync();

        var service = new AlertaService(db, new CurrentUserFake(esAdmin: true));

        var resultado = await service.ListarAsync();

        Assert.Contains(resultado.Alertas, a => a.Tipo == "StockCritico" && a.HotelId == 1);
        Assert.Contains(resultado.Alertas, a => a.Tipo == "CuentaVencida" && a.Entidad == "DocumentoCompra");
        Assert.Contains(resultado.Alertas, a => a.Tipo == "CierrePendiente");
        Assert.True(resultado.Resumen.Total >= 3);
    }

    [Fact]
    public async Task Listar_DigitadorNoRecibeAlertasFinancierasNiCierres()
    {
        using var db = TestDb.Crear();
        db.Proveedores.Single(p => p.Id == 1).DiasCredito = 0;
        db.StockMinimos.Add(new StockMinimo { HotelId = 1, ProductoId = 1, CantidadMinimaBase = 20 });
        TestDb.AgregarCompra(db, 1, "ALT-DIG", new DateOnly(2000, 1, 1), 10, 5);
        await db.SaveChangesAsync();

        var service = new AlertaService(db, new CurrentUserFake(hoteles: 1));

        var resultado = await service.ListarAsync();

        Assert.Contains(resultado.Alertas, a => a.Tipo == "StockCritico" && a.HotelId == 1);
        Assert.DoesNotContain(resultado.Alertas, a => a.Tipo == "CuentaVencida");
        Assert.DoesNotContain(resultado.Alertas, a => a.Tipo == "CierrePendiente");
    }

    [Fact]
    public async Task Listar_IgnoraCuentasVencidasImportadasHistoricas()
    {
        using var db = TestDb.Crear();
        db.Proveedores.Single(p => p.Id == 1).DiasCredito = 0;
        var historico = TestDb.AgregarCompra(db, 1, "ALT-HIST", new DateOnly(2000, 1, 1), 10, 5);
        historico.Observaciones = DocumentoCompra.ObservacionImportadoExcel;
        await db.SaveChangesAsync();

        var service = new AlertaService(db, new CurrentUserFake(esAdmin: true));

        var resultado = await service.ListarAsync();

        Assert.DoesNotContain(resultado.Alertas, a => a.Tipo == "CuentaVencida");
    }

    [Fact]
    public async Task Listar_IgnoraCuentasVencidasConProveedorDeImportacion()
    {
        using var db = TestDb.Crear();
        db.Proveedores.Add(new Proveedor { Id = 99, Nombre = Proveedor.NombreProveedorImportacionExcel });
        var historico = TestDb.AgregarCompra(db, 1, "ALT-PROV-HIST", new DateOnly(2000, 1, 1), 10, 5);
        historico.ProveedorId = 99;
        await db.SaveChangesAsync();

        var service = new AlertaService(db, new CurrentUserFake(esAdmin: true));

        var resultado = await service.ListarAsync();

        Assert.DoesNotContain(resultado.Alertas, a => a.Tipo == "CuentaVencida");
    }
}
