using StockControl.Application.Conteos;
using StockControl.Domain.Enums;

namespace StockControl.Application.Tests;

public class ConteoInventarioServiceTests
{
    [Fact]
    public async Task Crear_CongelaDiferenciaContraExistenciaDelSistema()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "CNT-001", new DateOnly(2026, 7, 1), 10, 5);

        var service = new ConteoInventarioService(db, new CurrentUserFake(esAdmin: true));

        var conteo = await service.CrearAsync(new CrearConteoInventarioRequest(
            new DateOnly(2026, 7, 2),
            1,
            "Conteo cocina",
            [new CrearConteoInventarioDetalleRequest(1, 8)]));

        var detalle = Assert.Single(conteo.Detalles);
        Assert.Equal(10m, detalle.CantidadSistemaBase);
        Assert.Equal(8m, detalle.CantidadFisicaBase);
        Assert.Equal(-2m, detalle.DiferenciaBase);
        Assert.Equal(-10m, detalle.ValorDiferenciaEstimado);
        Assert.Equal("Registrado", conteo.Estado);
    }

    [Fact]
    public async Task AplicarAjustes_CreaMovimientoDeAjusteYMarcaConteo()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "CNT-002", new DateOnly(2026, 7, 1), 10, 5);

        var service = new ConteoInventarioService(db, new CurrentUserFake(esAdmin: true));
        var conteo = await service.CrearAsync(new CrearConteoInventarioRequest(
            new DateOnly(2026, 7, 2),
            1,
            null,
            [new CrearConteoInventarioDetalleRequest(1, 12)]));

        var ajustado = await service.AplicarAjustesAsync(conteo.Id);

        Assert.NotNull(ajustado);
        Assert.Equal("Ajustado", ajustado.Estado);
        var movimiento = Assert.Single(db.Movimientos);
        Assert.Equal(TipoMovimiento.Ajuste, movimiento.Tipo);
        Assert.Equal(2m, movimiento.CantidadBase);
        Assert.Equal($"Conteo fisico #{conteo.Id}", movimiento.Referencia);
        Assert.NotNull(Assert.Single(ajustado.Detalles).MovimientoAjusteId);
    }

    [Fact]
    public async Task AplicarAjustes_DigitadorNoPuedeAplicar()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "CNT-003", new DateOnly(2026, 7, 1), 10, 5);

        var admin = new ConteoInventarioService(db, new CurrentUserFake(esAdmin: true));
        var conteo = await admin.CrearAsync(new CrearConteoInventarioRequest(
            new DateOnly(2026, 7, 2),
            1,
            null,
            [new CrearConteoInventarioDetalleRequest(1, 9)]));

        var digitador = new ConteoInventarioService(db, new CurrentUserFake(hoteles: 1));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => digitador.AplicarAjustesAsync(conteo.Id));
    }
}
