namespace StockControl.Application.Alertas;

public record AlertasResumenDto(
    int Total,
    int Criticas,
    int Altas,
    int Medias,
    int Bajas);

public record AlertaDto(
    string Id,
    string Tipo,
    string Severidad,
    string Titulo,
    string Mensaje,
    int? HotelId,
    string? Hotel,
    string? Entidad,
    int? EntidadId,
    decimal? Monto,
    DateOnly? Fecha,
    string? AccionSugerida);

public record AlertasResultadoDto(AlertasResumenDto Resumen, List<AlertaDto> Alertas);
