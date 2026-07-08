using StockControl.Application.CuentasPorPagar;
using StockControl.Domain.Entities;

namespace StockControl.Application.Tests;

public class CuentasPorPagarServiceTests
{
    [Fact]
    public async Task Listar_MuestraSaldoPendienteYVencido()
    {
        using var db = TestDb.Crear();
        db.Proveedores.Single(p => p.Id == 1).DiasCredito = 15;
        TestDb.AgregarCompra(db, 1, "CXP-001", new DateOnly(2000, 1, 1), 10, 5);
        await db.SaveChangesAsync();

        var service = new CuentasPorPagarService(db, new CurrentUserFake(esAdmin: true));

        var resultado = await service.ListarAsync(new FiltroCuentasPorPagar(null, null, null, null));
        var cuenta = Assert.Single(resultado.Cuentas);

        Assert.Equal(50m, cuenta.NetoAPagar);
        Assert.Equal(50m, cuenta.Saldo);
        Assert.Equal("Vencido", cuenta.Estado);
        Assert.Equal(new DateOnly(2000, 1, 16), cuenta.FechaVencimiento);
        Assert.Equal(50m, resultado.Resumen.SaldoVencido);
    }

    [Fact]
    public async Task Listar_IgnoraDocumentosImportadosHistoricos()
    {
        using var db = TestDb.Crear();
        db.Proveedores.Single(p => p.Id == 1).DiasCredito = 0;
        var historico = TestDb.AgregarCompra(db, 1, "HIST-CXP", new DateOnly(2000, 1, 1), 10, 5);
        historico.Observaciones = DocumentoCompra.ObservacionImportadoExcel;
        await db.SaveChangesAsync();

        var service = new CuentasPorPagarService(db, new CurrentUserFake(esAdmin: true));

        var resultado = await service.ListarAsync(new FiltroCuentasPorPagar(null, null, null, null));

        Assert.Empty(resultado.Cuentas);
        Assert.Equal(0m, resultado.Resumen.SaldoPendiente);
        Assert.Equal(0m, resultado.Resumen.SaldoVencido);
    }

    [Fact]
    public async Task RegistrarPago_NoPermitePagarDocumentosImportadosHistoricos()
    {
        using var db = TestDb.Crear();
        var historico = TestDb.AgregarCompra(db, 1, "HIST-PAGO", new DateOnly(2099, 1, 1), 10, 5);
        historico.Observaciones = DocumentoCompra.ObservacionImportadoExcel;
        await db.SaveChangesAsync();

        var service = new CuentasPorPagarService(db, new CurrentUserFake(esAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegistrarPagoAsync(new RegistrarPagoProveedorRequest(
                historico.Id,
                new DateOnly(2099, 1, 2),
                10,
                "Transferencia",
                null,
                null)));
    }

    [Fact]
    public async Task RegistrarPago_DejaCuentaParcialYLuegoPagada()
    {
        using var db = TestDb.Crear();
        var doc = TestDb.AgregarCompra(db, 1, "CXP-002", new DateOnly(2099, 1, 1), 10, 5);
        var service = new CuentasPorPagarService(db, new CurrentUserFake(esAdmin: true));

        await service.RegistrarPagoAsync(new RegistrarPagoProveedorRequest(doc.Id, new DateOnly(2099, 1, 2), 20, "Transferencia", "TRX-1", null));

        var parcial = Assert.Single((await service.ListarAsync(new FiltroCuentasPorPagar(null, null, null, null))).Cuentas);
        Assert.Equal(20m, parcial.Pagado);
        Assert.Equal(30m, parcial.Saldo);
        Assert.Equal("Parcial", parcial.Estado);

        await service.RegistrarPagoAsync(new RegistrarPagoProveedorRequest(doc.Id, new DateOnly(2099, 1, 3), 30, "Cheque", "CH-1", null));

        var resultado = await service.ListarAsync(new FiltroCuentasPorPagar(null, null, null, null, SoloPendientes: false));
        var pagada = Assert.Single(resultado.Cuentas);
        Assert.Equal(0m, pagada.Saldo);
        Assert.Equal("Pagado", pagada.Estado);
    }

    [Fact]
    public async Task RegistrarPago_NoPermiteSobrepago()
    {
        using var db = TestDb.Crear();
        var doc = TestDb.AgregarCompra(db, 1, "CXP-003", new DateOnly(2099, 1, 1), 10, 5);
        var service = new CuentasPorPagarService(db, new CurrentUserFake(esAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegistrarPagoAsync(new RegistrarPagoProveedorRequest(doc.Id, new DateOnly(2099, 1, 2), 51, "Transferencia", null, null)));
    }

    [Fact]
    public async Task Listar_ResumeAntiguedadDeSaldos()
    {
        using var db = TestDb.Crear();
        db.Proveedores.Single(p => p.Id == 1).DiasCredito = 0;
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        TestDb.AgregarCompra(db, 1, "CXP-POR-VENCER", hoy.AddDays(10), 3, 5);
        TestDb.AgregarCompra(db, 1, "CXP-0-30", hoy.AddDays(-10), 10, 5);
        TestDb.AgregarCompra(db, 1, "CXP-31-60", hoy.AddDays(-40), 2, 5);
        TestDb.AgregarCompra(db, 1, "CXP-61", hoy.AddDays(-70), 1, 5);

        var service = new CuentasPorPagarService(db, new CurrentUserFake(esAdmin: true));

        var resultado = await service.ListarAsync(new FiltroCuentasPorPagar(null, null, null, null));

        Assert.Equal(15m, resultado.Resumen.PorVencer);
        Assert.Equal(50m, resultado.Resumen.Vencido0A30);
        Assert.Equal(10m, resultado.Resumen.Vencido31A60);
        Assert.Equal(5m, resultado.Resumen.Vencido61Mas);
    }

    [Fact]
    public async Task EliminarPago_RestauraSaldoPendiente()
    {
        using var db = TestDb.Crear();
        var doc = TestDb.AgregarCompra(db, 1, "CXP-004", new DateOnly(2099, 1, 1), 10, 5);
        var service = new CuentasPorPagarService(db, new CurrentUserFake(esAdmin: true));
        var pago = await service.RegistrarPagoAsync(new RegistrarPagoProveedorRequest(
            doc.Id,
            new DateOnly(2099, 1, 2),
            20,
            "Transferencia",
            "TRX-DEL",
            null));

        var eliminado = await service.EliminarPagoAsync(pago.Id);

        var cuenta = Assert.Single((await service.ListarAsync(new FiltroCuentasPorPagar(null, null, null, null))).Cuentas);
        Assert.True(eliminado);
        Assert.Equal(0m, cuenta.Pagado);
        Assert.Equal(50m, cuenta.Saldo);
    }
}
