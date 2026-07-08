namespace StockControl.Application.Alertas;

public interface IAlertaService
{
    Task<AlertasResultadoDto> ListarAsync(CancellationToken ct = default);
    Task<AlertasResumenDto> ResumenAsync(CancellationToken ct = default);
}
