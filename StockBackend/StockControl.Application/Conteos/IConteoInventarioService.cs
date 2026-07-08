namespace StockControl.Application.Conteos;

public interface IConteoInventarioService
{
    Task<List<PlantillaConteoItemDto>> PlantillaAsync(int hotelId, DateOnly? fecha = null, CancellationToken ct = default);
    Task<List<ConteoInventarioResumenDto>> ListarAsync(FiltroConteos filtro, CancellationToken ct = default);
    Task<ConteoInventarioDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<ConteoInventarioDto> CrearAsync(CrearConteoInventarioRequest req, CancellationToken ct = default);
    Task<ConteoInventarioDto?> AplicarAjustesAsync(int id, CancellationToken ct = default);
}
