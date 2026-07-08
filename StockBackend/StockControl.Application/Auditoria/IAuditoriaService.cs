namespace StockControl.Application.Auditoria;

public interface IAuditoriaService
{
    Task<List<AuditoriaEventoDto>> ListarAsync(FiltroAuditoria filtro, CancellationToken ct = default);
    Task<AuditoriaEventoDto> RegistrarAsync(RegistrarAuditoriaRequest req, CancellationToken ct = default);
}
