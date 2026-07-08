using StockControl.Application.Compras;
using StockControl.Application.Conteos;
using StockControl.Application.CuentasPorPagar;
using StockControl.Application.Inventario;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Tests;

public class PeriodoCerradoGuardTests
{
    [Fact]
    public async Task Documento_NoPermiteCrearEnMesCerrado()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCierre(db, 1, 2026, 5);
        var service = new DocumentoCompraService(db, new CurrentUserFake(esAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CrearAsync(new CrearDocumentoCompraRequest(
            new DateOnly(2026, 5, 12),
            "LOCK-DOC",
            1,
            1,
            0,
            null,
            [new CrearDetalleCompraRequest(1, 1, 1, 5)])));

        var abierto = await service.CrearAsync(new CrearDocumentoCompraRequest(
            new DateOnly(2026, 6, 1),
            "OPEN-DOC",
            1,
            1,
            0,
            null,
            [new CrearDetalleCompraRequest(1, 1, 1, 5)]));

        Assert.Equal("OPEN-DOC", abierto.NumeroDocumento);
    }

    [Fact]
    public async Task Documento_PermiteCrearSiElCierreEstaAnulado()
    {
        using var db = TestDb.Crear();
        var cierre = TestDb.AgregarCierre(db, 1, 2026, 5);
        cierre.Estado = EstadoCierreMensual.Anulado;
        await db.SaveChangesAsync();
        var service = new DocumentoCompraService(db, new CurrentUserFake(esAdmin: true));

        var documento = await service.CrearAsync(new CrearDocumentoCompraRequest(
            new DateOnly(2026, 5, 12),
            "REOPEN-DOC",
            1,
            1,
            0,
            null,
            [new CrearDetalleCompraRequest(1, 1, 1, 5)]));

        Assert.Equal("REOPEN-DOC", documento.NumeroDocumento);
    }

    [Fact]
    public async Task Movimiento_NoPermiteRegistrarEnMesCerrado()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCierre(db, 1, 2026, 5);
        var service = new InventarioService(db, new CurrentUserFake(esAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegistrarMovimientoAsync(new CrearMovimientoRequest(
            "Merma",
            new DateOnly(2026, 5, 20),
            1,
            1,
            1,
            1,
            "Merma tardia")));
    }

    [Fact]
    public async Task Pago_NoPermiteRegistrarConFechaEnMesCerrado()
    {
        using var db = TestDb.Crear();
        var doc = TestDb.AgregarCompra(db, 1, "LOCK-PAY", new DateOnly(2026, 4, 20), 10, 5);
        TestDb.AgregarCierre(db, 1, 2026, 5);
        var service = new CuentasPorPagarService(db, new CurrentUserFake(esAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegistrarPagoAsync(new RegistrarPagoProveedorRequest(
            doc.Id,
            new DateOnly(2026, 5, 3),
            10,
            "Transferencia",
            null,
            null)));

        var pago = await service.RegistrarPagoAsync(new RegistrarPagoProveedorRequest(
            doc.Id,
            new DateOnly(2026, 6, 3),
            10,
            "Transferencia",
            null,
            null));

        Assert.Equal(new DateOnly(2026, 6, 3), pago.Fecha);
    }

    [Fact]
    public async Task Conteo_NoPermiteCrearNiAplicarAjustesEnMesCerrado()
    {
        using var db = TestDb.Crear();
        TestDb.AgregarCompra(db, 1, "LOCK-CNT", new DateOnly(2026, 5, 1), 10, 5);
        TestDb.AgregarCierre(db, 1, 2026, 5);
        var service = new ConteoInventarioService(db, new CurrentUserFake(esAdmin: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CrearAsync(new CrearConteoInventarioRequest(
            new DateOnly(2026, 5, 15),
            1,
            null,
            [new CrearConteoInventarioDetalleRequest(1, 8)])));

        var conteo = new ConteoInventario
        {
            HotelId = 1,
            Fecha = new DateOnly(2026, 5, 10),
            Estado = EstadoConteoInventario.Registrado,
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
        };
        db.ConteosInventario.Add(conteo);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AplicarAjustesAsync(conteo.Id));
    }
}
