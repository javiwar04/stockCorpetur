using Microsoft.EntityFrameworkCore;
using StockControl.Application.Cierres;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Tests;

public class CierreMensualServiceTests
{
    [Fact]
    public async Task Preview_CalculaComprasInventarioMermasConteosYCuentas()
    {
        using var db = TestDb.Crear();
        db.Proveedores.Single(p => p.Id == 1).DiasCredito = 0;

        var doc = TestDb.AgregarCompra(db, 1, "CIE-001", new DateOnly(2026, 5, 1), 10, 5);
        db.StockMinimos.Add(new StockMinimo { HotelId = 1, ProductoId = 1, CantidadMinimaBase = 20 });
        db.Movimientos.Add(new MovimientoInventario
        {
            HotelId = 1,
            ProductoId = 1,
            Tipo = TipoMovimiento.Merma,
            Fecha = new DateOnly(2026, 5, 5),
            CantidadBase = 2,
        });
        db.ConteosInventario.Add(new ConteoInventario
        {
            HotelId = 1,
            Fecha = new DateOnly(2026, 5, 10),
            Estado = EstadoConteoInventario.Registrado,
            Detalles =
            {
                new ConteoInventarioDetalle
                {
                    ProductoId = 1,
                    CantidadSistemaBase = 8,
                    CantidadFisicaBase = 7,
                    DiferenciaBase = -1,
                    ValorDiferenciaEstimado = -5,
                },
            },
        });
        db.PagosProveedor.Add(new PagoProveedor
        {
            DocumentoCompraId = doc.Id,
            ProveedorId = 1,
            Fecha = new DateOnly(2026, 5, 15),
            Monto = 10,
        });
        await db.SaveChangesAsync();

        var service = new CierreMensualService(db, new CurrentUserFake(esAdmin: true));

        var cierre = await service.PreviewAsync(1, 2026, 5);

        Assert.Equal("Preliminar", cierre.Estado);
        Assert.Equal(50m, cierre.ComprasTotal);
        Assert.Equal(1, cierre.DocumentosCompra);
        Assert.Equal(40m, cierre.ValorInventarioEstimado);
        Assert.Equal(1, cierre.ProductosEnRiesgo);
        Assert.Equal(60m, cierre.ValorFaltanteEstimado);
        Assert.Equal(10m, cierre.ValorMermasEstimado);
        Assert.Equal(1, cierre.MovimientosMerma);
        Assert.Equal(1, cierre.ConteosFisicos);
        Assert.Equal(5m, cierre.ValorDiferenciasConteo);
        Assert.Equal(40m, cierre.SaldoCuentasPorPagar);
        Assert.Equal(40m, cierre.SaldoCuentasVencido);
        Assert.Equal(1, cierre.DocumentosVencidos);
    }

    [Fact]
    public async Task Preview_IgnoraImportadosHistoricosEnCuentasPorPagar()
    {
        using var db = TestDb.Crear();
        db.Proveedores.Single(p => p.Id == 1).DiasCredito = 0;
        var historico = TestDb.AgregarCompra(db, 1, "CIE-HIST", new DateOnly(2026, 5, 1), 10, 5);
        historico.Observaciones = DocumentoCompra.ObservacionImportadoExcel;
        await db.SaveChangesAsync();

        var service = new CierreMensualService(db, new CurrentUserFake(esAdmin: true));

        var cierre = await service.PreviewAsync(1, 2026, 5);

        Assert.Equal(50m, cierre.ComprasTotal);
        Assert.Equal(50m, cierre.ValorInventarioEstimado);
        Assert.Equal(0m, cierre.SaldoCuentasPorPagar);
        Assert.Equal(0m, cierre.SaldoCuentasVencido);
        Assert.Equal(0, cierre.DocumentosVencidos);
    }

    [Fact]
    public async Task Cerrar_GuardaSnapshotYNoPermiteDuplicarPeriodo()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "CIE-002", new DateOnly(2026, 6, 1), 3, 7);

        var service = new CierreMensualService(db, new CurrentUserFake(esGerencia: true));

        var cierre = await service.CerrarAsync(new CerrarMesRequest(1, 2026, 6, "Cierre junio"));

        Assert.True(cierre.Id > 0);
        Assert.Equal("Cerrado", cierre.Estado);
        Assert.Equal(21m, cierre.ComprasTotal);
        Assert.Equal("Cierre junio", cierre.Observaciones);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CerrarAsync(new CerrarMesRequest(1, 2026, 6, null)));
    }

    [Fact]
    public async Task Anular_PermiteCerrarDeNuevoElPeriodo()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "CIE-REOPEN", new DateOnly(2026, 7, 1), 3, 7);

        var service = new CierreMensualService(db, new CurrentUserFake(esAdmin: true));
        var primero = await service.CerrarAsync(new CerrarMesRequest(1, 2026, 7, "Primer cierre"));

        var anulado = await service.AnularAsync(primero.Id, new AnularCierreMensualRequest("Factura pendiente"));
        var segundo = await service.CerrarAsync(new CerrarMesRequest(1, 2026, 7, "Cierre corregido"));

        Assert.NotNull(anulado);
        Assert.Equal("Anulado", anulado.Estado);
        Assert.Contains("Factura pendiente", anulado.Observaciones);
        Assert.NotEqual(primero.Id, segundo.Id);
        Assert.Equal("Cerrado", segundo.Estado);
        Assert.Equal(2, db.CierresMensuales.Count(c => c.HotelId == 1 && c.Anio == 2026 && c.Mes == 7));
    }

    [Fact]
    public async Task Preview_DigitadorNoPuedeConsultarCierres()
    {
        using var db = TestDb.Crear();
        var service = new CierreMensualService(db, new CurrentUserFake(hoteles: 1));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PreviewAsync(1, 2026, 5));
    }
}
