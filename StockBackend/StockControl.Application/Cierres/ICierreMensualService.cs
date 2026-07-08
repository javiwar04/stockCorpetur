namespace StockControl.Application.Cierres;

public interface ICierreMensualService
{
    Task<List<CierreMensualDto>> ListarAsync(FiltroCierresMensuales filtro, CancellationToken ct = default);
    Task<CierreMensualDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<CierreMensualDto> PreviewAsync(int hotelId, int anio, int mes, CancellationToken ct = default);
    Task<CierreMensualDto> CerrarAsync(CerrarMesRequest req, CancellationToken ct = default);
    Task<CierreMensualDto?> AnularAsync(int id, AnularCierreMensualRequest req, CancellationToken ct = default);
}
