using ClosedXML.Excel;
using StockControl.Application.Reportes;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;
using StockControl.Infrastructure.Reportes;

namespace StockControl.Application.Tests;

public class ReporteServiceTests
{
    [Fact]
    public async Task Excel_IncluyeLiquidacionYFiltraProveedor()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "P1-001", new DateOnly(2026, 7, 1), 10, 5);
        db.Proveedores.Add(new Proveedor { Id = 2, Nombre = "Proveedor Alterno", Nit = "ALT-1" });
        db.Documentos.Add(new DocumentoCompra
        {
            Fecha = new DateOnly(2026, 7, 2),
            NumeroDocumento = "P2-001",
            NumeroPedido = "P2-001",
            HotelId = 1,
            ProveedorId = 2,
            Estado = EstadoDocumentoCompra.Recibido,
            Detalles =
            {
                new DetalleCompra { ProductoId = 1, UnidadId = 1, Cantidad = 7, PrecioUnitario = 9, FactorABase = 1m },
            },
        });
        db.SaveChanges();

        var service = new ReporteService(db, new CurrentUserFake(esAdmin: true));
        var bytes = await service.GenerarExcelAsync(new FiltroReporte(null, 1, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)));

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        Assert.True(libro.Worksheets.Contains("Liquidacion proveedores"));
        Assert.True(libro.Worksheets.Contains("Facturas proveedor"));

        var facturas = libro.Worksheet("Facturas proveedor");
        Assert.Equal("Proveedor Test", facturas.Cell(2, 2).GetString());
        Assert.True(facturas.Cell(3, 2).IsEmpty());
    }

    [Fact]
    public async Task Pdf_SeGeneraConLiquidacionProveedor()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "PDF-001", new DateOnly(2026, 7, 1), 10, 5);

        var service = new ReporteService(db, new CurrentUserFake(esAdmin: true));
        var bytes = await service.GenerarPdfAsync(new FiltroReporte(null, null, null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)));

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task KardexExcel_IncluyeSaldoAcumulado()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "KDX-XLS", new DateOnly(2026, 7, 1), 10, 5);
        db.Movimientos.Add(new MovimientoInventario
        {
            HotelId = 1,
            ProductoId = 1,
            Tipo = TipoMovimiento.Salida,
            Fecha = new DateOnly(2026, 7, 2),
            CantidadBase = 3,
            Referencia = "Cocina",
        });
        db.SaveChanges();

        var service = new ReporteService(db, new CurrentUserFake(esAdmin: true));
        var bytes = await service.GenerarKardexExcelAsync(new FiltroReporteKardex(1, 1, null, null));

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var kardex = libro.Worksheet("Kardex");

        Assert.Equal("Compra", kardex.Cell(2, 2).GetString());
        Assert.Equal(10m, kardex.Cell(2, 9).GetValue<decimal>());
        Assert.Equal("Salida", kardex.Cell(3, 2).GetString());
        Assert.Equal(7m, kardex.Cell(3, 9).GetValue<decimal>());
    }

    [Fact]
    public async Task CuentasPorPagarExcel_IncluyeFacturasYPagos()
    {
        using var db = TestDb.Crear();
        var doc = TestDb.AgregarCompra(db, 1, "CXP-XLS", new DateOnly(2026, 7, 1), 10, 5);
        db.PagosProveedor.Add(new PagoProveedor
        {
            DocumentoCompraId = doc.Id,
            ProveedorId = 1,
            Fecha = new DateOnly(2026, 7, 2),
            Monto = 20,
            MetodoPago = "Transferencia",
            Referencia = "TRX",
        });
        db.SaveChanges();

        var service = new ReporteService(db, new CurrentUserFake(esAdmin: true));
        var bytes = await service.GenerarCuentasPorPagarExcelAsync(
            new FiltroReporteCuentasPorPagar(null, null, null, null, SoloPendientes: false));

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        Assert.True(libro.Worksheets.Contains("Facturas"));
        Assert.True(libro.Worksheets.Contains("Pagos"));
        Assert.True(libro.Worksheets.Contains("Por proveedor"));

        var facturas = libro.Worksheet("Facturas");
        var pagos = libro.Worksheet("Pagos");

        Assert.Equal("CXP-XLS", facturas.Cell(2, 4).GetString());
        Assert.Equal(30m, facturas.Cell(2, 12).GetValue<decimal>());
        Assert.Equal("TRX", pagos.Cell(2, 6).GetString());
        Assert.Equal(20m, pagos.Cell(2, 8).GetValue<decimal>());
    }

    [Fact]
    public async Task ConteosExcel_IncluyeResumenYDetalle()
    {
        using var db = TestDb.Crear();
        db.ConteosInventario.Add(new ConteoInventario
        {
            Fecha = new DateOnly(2026, 7, 5),
            HotelId = 1,
            Estado = EstadoConteoInventario.Ajustado,
            Observaciones = "Conteo mensual",
            Detalles =
            {
                new ConteoInventarioDetalle
                {
                    ProductoId = 1,
                    CantidadSistemaBase = 10,
                    CantidadFisicaBase = 8,
                    DiferenciaBase = -2,
                    ValorDiferenciaEstimado = -10,
                },
            },
        });
        await db.SaveChangesAsync();

        var service = new ReporteService(db, new CurrentUserFake(esAdmin: true));
        var bytes = await service.GenerarConteosExcelAsync(new FiltroReporteConteos(1, null, null));

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        Assert.True(libro.Worksheets.Contains("Resumen"));
        Assert.True(libro.Worksheets.Contains("Conteos"));
        Assert.True(libro.Worksheets.Contains("Detalle"));

        var detalle = libro.Worksheet("Detalle");
        Assert.Equal("Tomate", detalle.Cell(2, 5).GetString());
        Assert.Equal(-2m, detalle.Cell(2, 9).GetValue<decimal>());
        Assert.Equal(-10m, detalle.Cell(2, 11).GetValue<decimal>());
    }

    [Fact]
    public async Task ConteosPdf_SeGeneraConFormatoPdf()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        using var db = TestDb.Crear();
        db.ConteosInventario.Add(new ConteoInventario
        {
            Fecha = new DateOnly(2026, 7, 5),
            HotelId = 1,
            Estado = EstadoConteoInventario.Registrado,
            Detalles =
            {
                new ConteoInventarioDetalle
                {
                    ProductoId = 1,
                    CantidadSistemaBase = 10,
                    CantidadFisicaBase = 12,
                    DiferenciaBase = 2,
                    ValorDiferenciaEstimado = 10,
                },
            },
        });
        await db.SaveChangesAsync();

        var service = new ReporteService(db, new CurrentUserFake(esAdmin: true));
        var bytes = await service.GenerarConteosPdfAsync(new FiltroReporteConteos(null, null, null));

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task CierresMensualesExcel_IncluyeResumenCierresYPorHotel()
    {
        using var db = TestDb.Crear();
        var cierre = TestDb.AgregarCierre(db, 1, 2026, 5);
        cierre.ComprasTotal = 100;
        cierre.DocumentosCompra = 2;
        cierre.ValorInventarioEstimado = 80;
        cierre.ProductosEnRiesgo = 1;
        cierre.SaldoCuentasPorPagar = 50;
        cierre.SaldoCuentasVencido = 30;
        cierre.DocumentosVencidos = 1;
        cierre.Observaciones = "Cierre mayo";
        await db.SaveChangesAsync();

        var service = new ReporteService(db, new CurrentUserFake(esAdmin: true));
        var bytes = await service.GenerarCierresMensualesExcelAsync(new FiltroReporteCierresMensuales(1, 2026, 5));

        using var libro = new XLWorkbook(new MemoryStream(bytes));
        Assert.True(libro.Worksheets.Contains("Resumen"));
        Assert.True(libro.Worksheets.Contains("Cierres"));
        Assert.True(libro.Worksheets.Contains("Por hotel"));

        var cierres = libro.Worksheet("Cierres");
        Assert.Equal("Hotel Uno", cierres.Cell(2, 2).GetString());
        Assert.Equal(2026, cierres.Cell(2, 3).GetValue<int>());
        Assert.Equal(5, cierres.Cell(2, 4).GetValue<int>());
        Assert.Equal(100m, cierres.Cell(2, 7).GetValue<decimal>());
        Assert.Equal("Cierre mayo", cierres.Cell(2, 22).GetString());
    }

    [Fact]
    public async Task CierresMensualesPdf_SeGeneraConFormatoPdf()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        using var db = TestDb.Crear();
        var cierre = TestDb.AgregarCierre(db, 1, 2026, 5);
        cierre.ComprasTotal = 100;
        cierre.ValorInventarioEstimado = 80;
        await db.SaveChangesAsync();

        var service = new ReporteService(db, new CurrentUserFake(esAdmin: true));
        var bytes = await service.GenerarCierresMensualesPdfAsync(new FiltroReporteCierresMensuales(null, 2026, null));

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
