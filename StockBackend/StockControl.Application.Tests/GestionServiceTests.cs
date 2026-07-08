using StockControl.Application.Gestion;

namespace StockControl.Application.Tests;

public class GestionServiceTests
{
    [Fact]
    public async Task UpsertComensal_CreaYLuegoActualiza()
    {
        using var db = TestDb.Crear();
        var service = new GestionService(db);

        await service.UpsertComensalAsync(new UpsertComensalRequest(1, 2026, 5, 1000));
        await service.UpsertComensalAsync(new UpsertComensalRequest(1, 2026, 5, 1200)); // corrige

        var lista = await service.ListarComensalesAsync(2026, 5);
        var registro = Assert.Single(lista);
        Assert.Equal(1200, registro.NumeroComensales);
    }

    [Fact]
    public async Task UpsertPresupuesto_CreaYLuegoActualiza()
    {
        using var db = TestDb.Crear();
        var service = new GestionService(db);

        await service.UpsertPresupuestoAsync(new UpsertPresupuestoRequest(1, "Verdura", 2026, 5, 10000));
        await service.UpsertPresupuestoAsync(new UpsertPresupuestoRequest(1, "Verdura", 2026, 5, 12000));

        var lista = await service.ListarPresupuestosAsync(2026, 5);
        var registro = Assert.Single(lista);
        Assert.Equal(12000m, registro.Monto);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task MesInvalido_Falla(int mes)
    {
        using var db = TestDb.Crear();
        var service = new GestionService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpsertComensalAsync(new UpsertComensalRequest(1, 2026, mes, 100)));
    }

    [Fact]
    public async Task CategoriaInvalida_Falla()
    {
        using var db = TestDb.Crear();
        var service = new GestionService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpsertPresupuestoAsync(new UpsertPresupuestoRequest(1, "NoExiste", 2026, 5, 100)));
    }

    [Fact]
    public async Task ComensalesNegativos_Falla()
    {
        using var db = TestDb.Crear();
        var service = new GestionService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpsertComensalAsync(new UpsertComensalRequest(1, 2026, 5, -1)));
    }
}
