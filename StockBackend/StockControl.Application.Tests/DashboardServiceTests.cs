using StockControl.Application.Dashboard;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Tests;

public class DashboardServiceTests
{
    [Fact]
    public async Task Resumen_CalculaGastoVariacionYFoodCost()
    {
        using var db = TestDb.Crear();
        // Abril: Q200. Mayo: Q500. Comensales mayo: 100. Presupuesto mayo: Q1000.
        TestDb.AgregarCompra(db, 1, "ABR-01", new DateOnly(2026, 4, 10), 40, 5);
        TestDb.AgregarCompra(db, 1, "MAY-01", new DateOnly(2026, 5, 10), 100, 5);
        db.Comensales.Add(new ComensalMensual { HotelId = 1, Anio = 2026, Mes = 5, NumeroComensales = 100 });
        db.Presupuestos.Add(new PresupuestoMensual
        {
            HotelId = 1, Categoria = CategoriaProducto.Verdura, Anio = 2026, Mes = 5, Monto = 1000,
        });
        await db.SaveChangesAsync();

        var service = new DashboardService(db, new CurrentUserFake(esAdmin: true));
        var resumen = await service.ResumenMensualAsync(2026, 5);

        Assert.Equal(500m, resumen.GastoTotal);
        Assert.Equal(200m, resumen.GastoMesAnterior);
        Assert.Equal(150m, resumen.VariacionPorcentaje); // (500-200)/200

        var hotel = Assert.Single(resumen.PorHotel);
        Assert.Equal(100, hotel.Comensales);
        Assert.Equal(5m, hotel.CostoPorComensal);      // 500 / 100
        Assert.Equal(1000m, hotel.Presupuesto);
        Assert.Equal(50m, hotel.PorcentajePresupuesto); // 500 / 1000
    }

    [Fact]
    public async Task Alertas_DetectaIncrementoSobreElUmbral()
    {
        using var db = TestDb.Crear();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        // Referencia (ventana de 90 días previos a los últimos 30): Q5.
        TestDb.AgregarCompra(db, 1, "REF-01", hoy.AddDays(-60), 100, 5);
        // Reciente (últimos 30 días): Q6.5 → +30%.
        TestDb.AgregarCompra(db, 1, "REC-01", hoy.AddDays(-5), 10, 6.5m);

        var service = new DashboardService(db, new CurrentUserFake(esAdmin: true));
        var alertas = await service.AlertasPrecioAsync(umbralPorcentaje: 15);

        var alerta = Assert.Single(alertas);
        Assert.Equal("Tomate", alerta.Producto);
        Assert.Equal(6.5m, alerta.PrecioReciente);
        Assert.Equal(5m, alerta.PrecioReferencia);
        Assert.Equal(30m, alerta.IncrementoPorcentaje);
    }

    [Fact]
    public async Task Alertas_IncrementoBajoElUmbral_NoAlerta()
    {
        using var db = TestDb.Crear();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        TestDb.AgregarCompra(db, 1, "REF-02", hoy.AddDays(-60), 100, 5);
        TestDb.AgregarCompra(db, 1, "REC-02", hoy.AddDays(-5), 10, 5.5m); // +10%

        var service = new DashboardService(db, new CurrentUserFake(esAdmin: true));
        var alertas = await service.AlertasPrecioAsync(umbralPorcentaje: 15);

        Assert.Empty(alertas);
    }

    [Fact]
    public async Task Alertas_SinHistorialDeReferencia_NoAlerta()
    {
        using var db = TestDb.Crear();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        // Solo compra reciente, sin referencia previa: no hay contra qué comparar.
        TestDb.AgregarCompra(db, 1, "REC-03", hoy.AddDays(-5), 10, 99);

        var service = new DashboardService(db, new CurrentUserFake(esAdmin: true));
        var alertas = await service.AlertasPrecioAsync(umbralPorcentaje: 15);

        Assert.Empty(alertas);
    }

    [Fact]
    public async Task TopComprados_NormalizaCajasALibras()
    {
        using var db = TestDb.Crear();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        // 2 cajas (factor 25) = 50 lb + 30 lb sueltas = 80 lb.
        var doc = new DocumentoCompra
        {
            Fecha = hoy, NumeroDocumento = "MIX-01", NumeroPedido = "MIX-01", HotelId = 1, ProveedorId = 1,
            Detalles =
            {
                new DetalleCompra { ProductoId = 1, UnidadId = 2, Cantidad = 2, PrecioUnitario = 150, FactorABase = 25 },
                new DetalleCompra { ProductoId = 1, UnidadId = 1, Cantidad = 30, PrecioUnitario = 6, FactorABase = 1 },
            },
        };
        db.Documentos.Add(doc);
        await db.SaveChangesAsync();

        var service = new DashboardService(db, new CurrentUserFake(esAdmin: true));
        var top = await service.TopCompradosAsync(meses: 2, top: 10);

        var tomate = Assert.Single(top);
        Assert.Equal(80m, tomate.CantidadBase);
        Assert.Equal(480m, tomate.GastoTotal);          // 300 + 180
        Assert.Equal(6m, tomate.PrecioPromedioBase);    // 480 / 80
    }

    [Fact]
    public async Task Digitador_SoloVeMetricasDeSuHotel()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "H1-M", new DateOnly(2026, 5, 10), 100, 5); // Q500
        TestDb.AgregarCompra(db, 2, "H2-M", new DateOnly(2026, 5, 10), 10, 5);  // Q50

        var service = new DashboardService(db, new CurrentUserFake(hoteles: 2));
        var resumen = await service.ResumenMensualAsync(2026, 5);

        Assert.Equal(50m, resumen.GastoTotal);
        var hotel = Assert.Single(resumen.PorHotel);
        Assert.Equal(2, hotel.HotelId);
    }

    [Fact]
    public async Task Resumen_FiltroHotel_AcotaMetricasDelAdmin()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "H1-F", new DateOnly(2026, 5, 10), 100, 5); // Q500
        TestDb.AgregarCompra(db, 2, "H2-F", new DateOnly(2026, 5, 10), 10, 5);  // Q50

        var service = new DashboardService(db, new CurrentUserFake(esAdmin: true));
        var resumen = await service.ResumenMensualAsync(2026, 5, hotelId: 2);

        Assert.Equal(50m, resumen.GastoTotal);
        var hotel = Assert.Single(resumen.PorHotel);
        Assert.Equal(2, hotel.HotelId);
    }

    [Fact]
    public async Task Resumen_FiltroHotelNoPermitido_LanzaUnauthorized()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "H1-X", new DateOnly(2026, 5, 10), 100, 5);
        TestDb.AgregarCompra(db, 2, "H2-X", new DateOnly(2026, 5, 10), 10, 5);

        var service = new DashboardService(db, new CurrentUserFake(hoteles: 1));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ResumenMensualAsync(2026, 5, hotelId: 2));
    }

    [Fact]
    public async Task Gerencial_CalculaInventarioMermasStockCriticoYCuentas()
    {
        using var db = TestDb.Crear();
        db.Proveedores.Single(p => p.Id == 1).DiasCredito = 0;
        var doc = TestDb.AgregarCompra(db, 1, "GER-01", new DateOnly(2026, 5, 1), 10, 5);
        db.StockMinimos.Add(new StockMinimo { HotelId = 1, ProductoId = 1, CantidadMinimaBase = 20 });
        db.Movimientos.Add(new MovimientoInventario
        {
            HotelId = 1,
            ProductoId = 1,
            Tipo = TipoMovimiento.Merma,
            Fecha = new DateOnly(2026, 5, 3),
            CantidadBase = 2,
            Referencia = "Merma test",
        });
        db.PagosProveedor.Add(new PagoProveedor
        {
            DocumentoCompraId = doc.Id,
            ProveedorId = 1,
            Fecha = new DateOnly(2026, 5, 5),
            Monto = 10,
            MetodoPago = "Transferencia",
        });
        await db.SaveChangesAsync();

        var service = new DashboardService(db, new CurrentUserFake(esAdmin: true));
        var gerencial = await service.GerencialAsync(2026, 5);

        Assert.Equal(40m, gerencial.ValorInventarioEstimado); // (10 - 2) * Q5
        Assert.Equal(10m, gerencial.ValorMermasEstimado);
        Assert.Equal(1, gerencial.MovimientosMerma);
        Assert.Equal(1, gerencial.ProductosEnRiesgo);
        Assert.Equal(60m, gerencial.ValorFaltanteEstimado); // faltan 12 lb * Q5
        Assert.True(gerencial.IncluyeFinanzas);
        Assert.Equal(40m, gerencial.SaldoCuentasPorPagar);
        Assert.Equal(40m, gerencial.SaldoCuentasVencido);
        Assert.Single(gerencial.TopProveedoresSaldo);
    }

    [Fact]
    public async Task Gerencial_IgnoraImportadosHistoricosEnFinanzasPeroConservaInventario()
    {
        using var db = TestDb.Crear();
        db.Proveedores.Single(p => p.Id == 1).DiasCredito = 0;
        var historico = TestDb.AgregarCompra(db, 1, "GER-HIST", new DateOnly(2026, 5, 1), 10, 5);
        historico.Observaciones = DocumentoCompra.ObservacionImportadoExcel;
        await db.SaveChangesAsync();

        var service = new DashboardService(db, new CurrentUserFake(esAdmin: true));

        var gerencial = await service.GerencialAsync(2026, 5);

        Assert.Equal(50m, gerencial.ValorInventarioEstimado);
        Assert.True(gerencial.IncluyeFinanzas);
        Assert.Equal(0m, gerencial.SaldoCuentasPorPagar);
        Assert.Equal(0m, gerencial.SaldoCuentasVencido);
        Assert.Empty(gerencial.TopProveedoresSaldo);
    }

    [Fact]
    public async Task Gerencial_DigitadorNoRecibeMetricasFinancieras()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "GER-02", new DateOnly(2026, 5, 1), 10, 5);

        var service = new DashboardService(db, new CurrentUserFake(hoteles: 1));
        var gerencial = await service.GerencialAsync(2026, 5);

        Assert.False(gerencial.IncluyeFinanzas);
        Assert.Null(gerencial.SaldoCuentasPorPagar);
        Assert.Null(gerencial.SaldoCuentasVencido);
        Assert.Empty(gerencial.TopProveedoresSaldo);
    }
}
