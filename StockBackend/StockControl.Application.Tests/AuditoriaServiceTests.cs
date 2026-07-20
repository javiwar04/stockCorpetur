using StockControl.Application.Auditoria;
using StockControl.Application.Compras;

namespace StockControl.Application.Tests;

public class AuditoriaServiceTests
{
    [Fact]
    public async Task RegistrarYListar_GuardaEventoConHotelYUsuario()
    {
        using var db = TestDb.Crear();
        var currentUser = new CurrentUserFake(esAdmin: true);
        var service = new AuditoriaService(db, currentUser);

        await service.RegistrarAsync(new RegistrarAuditoriaRequest(
            "Prueba",
            "EntidadTest",
            123,
            1,
            "Evento de prueba",
            "Detalle"));

        var eventos = await service.ListarAsync(new FiltroAuditoria(1, null, null, null, null));
        var evento = Assert.Single(eventos);

        Assert.Equal("tester", evento.Usuario);
        Assert.Equal("Prueba", evento.Accion);
        Assert.Equal("Hotel Uno", evento.Hotel);
        Assert.Equal("Evento de prueba", evento.Resumen);
    }

    [Fact]
    public async Task Listar_DigitadorNoPuedeConsultar()
    {
        using var db = TestDb.Crear();
        var service = new AuditoriaService(db, new CurrentUserFake(hoteles: 1));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ListarAsync(new FiltroAuditoria(null, null, null, null, null)));
    }

    [Fact]
    public async Task DocumentoCompra_RegistraEventoAlCrear()
    {
        using var db = TestDb.Crear();
        var currentUser = new CurrentUserFake(esAdmin: true);
        var auditoria = new AuditoriaService(db, currentUser);
        var service = new DocumentoCompraService(db, currentUser, auditoria: auditoria);

        await service.CrearAsync(new CrearDocumentoCompraRequest(
            new DateOnly(2026, 7, 8),
            "AUD-001",
            "PED-AUD-001",
            1,
            1,
            0,
            null,
            [new CrearDetalleCompraRequest(1, 1, 2, 5)]));

        var evento = Assert.Single(db.AuditoriaEventos);
        Assert.Equal("Documento creado", evento.Accion);
        Assert.Equal("DocumentoCompra", evento.Entidad);
        Assert.Equal(1, evento.HotelId);
        Assert.Contains("AUD-001", evento.Resumen);
    }
}
