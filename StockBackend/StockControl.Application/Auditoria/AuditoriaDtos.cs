namespace StockControl.Application.Auditoria;

public record FiltroAuditoria(
    int? HotelId,
    string? Accion,
    string? Entidad,
    DateOnly? Desde,
    DateOnly? Hasta);

public record RegistrarAuditoriaRequest(
    string Accion,
    string Entidad,
    int? EntidadId,
    int? HotelId,
    string Resumen,
    string? Detalle);

public record AuditoriaEventoDto(
    int Id,
    DateTime Fecha,
    string Usuario,
    string Accion,
    string Entidad,
    int? EntidadId,
    int? HotelId,
    string? Hotel,
    string Resumen,
    string? Detalle);
