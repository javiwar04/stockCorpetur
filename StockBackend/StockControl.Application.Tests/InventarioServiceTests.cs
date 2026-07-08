using StockControl.Application.Inventario;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Tests;

public class InventarioServiceTests
{
    [Fact]
    public async Task Kardex_MezclaComprasYMovimientosConSaldoAcumulado()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "KDX-001", new DateOnly(2026, 1, 1), 10, 5);
        db.Movimientos.Add(new MovimientoInventario
        {
            HotelId = 1,
            ProductoId = 1,
            Tipo = TipoMovimiento.Salida,
            Fecha = new DateOnly(2026, 1, 3),
            CantidadBase = 4,
            Referencia = "Cocina",
        });
        await db.SaveChangesAsync();

        var service = new InventarioService(db, new CurrentUserFake(esAdmin: true));

        var kardex = await service.KardexAsync(new FiltroKardex(1, 1, null, null));

        Assert.Equal(0m, kardex.SaldoInicial);
        Assert.Equal(10m, kardex.TotalEntradas);
        Assert.Equal(4m, kardex.TotalSalidas);
        Assert.Equal(6m, kardex.SaldoFinal);
        Assert.Equal(2, kardex.Movimientos.Count);
        Assert.Equal("Compra", kardex.Movimientos[0].Tipo);
        Assert.Equal(10m, kardex.Movimientos[0].Saldo);
        Assert.Equal("Salida", kardex.Movimientos[1].Tipo);
        Assert.Equal(6m, kardex.Movimientos[1].Saldo);
    }

    [Fact]
    public async Task Kardex_RespetaSaldoInicialCuandoHayFiltroDesde()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "KDX-002", new DateOnly(2025, 12, 31), 10, 5);
        db.Movimientos.AddRange(
            new MovimientoInventario
            {
                HotelId = 1,
                ProductoId = 1,
                Tipo = TipoMovimiento.Salida,
                Fecha = new DateOnly(2026, 1, 2),
                CantidadBase = 3,
                Referencia = "Cocina",
            },
            new MovimientoInventario
            {
                HotelId = 1,
                ProductoId = 1,
                Tipo = TipoMovimiento.Ajuste,
                Fecha = new DateOnly(2026, 1, 4),
                CantidadBase = -1,
                Referencia = "Conteo fisico",
            });
        await db.SaveChangesAsync();

        var service = new InventarioService(db, new CurrentUserFake(esAdmin: true));

        var kardex = await service.KardexAsync(new FiltroKardex(
            1,
            1,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31)));

        Assert.Equal(10m, kardex.SaldoInicial);
        Assert.Equal(3m, kardex.TotalSalidas);
        Assert.Equal(-1m, kardex.TotalAjustes);
        Assert.Equal(6m, kardex.SaldoFinal);
        Assert.Equal(2, kardex.Movimientos.Count);
    }
}
