namespace StockControl.Domain.Common;

/// <summary>
/// Base para todas las entidades: identidad y campos de auditoría.
/// Los campos de auditoría los rellena la Infrastructure (interceptor de EF Core).
/// </summary>
public abstract class EntidadBase
{
    public int Id { get; set; }

    public DateTime CreadoEn { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime? ModificadoEn { get; set; }
    public string? ModificadoPor { get; set; }
}
