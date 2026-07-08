namespace StockControl.Application.Reportes;

public record FiltroReporte(int? HotelId, int? ProveedorId, DateOnly? Desde, DateOnly? Hasta);
public record FiltroReporteKardex(int HotelId, int ProductoId, DateOnly? Desde, DateOnly? Hasta);
public record FiltroReporteCuentasPorPagar(
    int? HotelId,
    int? ProveedorId,
    DateOnly? Desde,
    DateOnly? Hasta,
    bool SoloPendientes = true);
public record FiltroReporteConteos(int? HotelId, DateOnly? Desde, DateOnly? Hasta);
public record FiltroReporteCierresMensuales(int? HotelId, int? Anio, int? Mes);

/// <summary>Genera reportes de compras exportables. Respeta el scoping por hotel del usuario.</summary>
public interface IReporteService
{
    Task<byte[]> GenerarExcelAsync(FiltroReporte filtro, CancellationToken ct = default);
    Task<byte[]> GenerarPdfAsync(FiltroReporte filtro, CancellationToken ct = default);
    Task<byte[]> GenerarKardexExcelAsync(FiltroReporteKardex filtro, CancellationToken ct = default);
    Task<byte[]> GenerarCuentasPorPagarExcelAsync(FiltroReporteCuentasPorPagar filtro, CancellationToken ct = default);
    Task<byte[]> GenerarConteosExcelAsync(FiltroReporteConteos filtro, CancellationToken ct = default);
    Task<byte[]> GenerarConteosPdfAsync(FiltroReporteConteos filtro, CancellationToken ct = default);
    Task<byte[]> GenerarCierresMensualesExcelAsync(FiltroReporteCierresMensuales filtro, CancellationToken ct = default);
    Task<byte[]> GenerarCierresMensualesPdfAsync(FiltroReporteCierresMensuales filtro, CancellationToken ct = default);
}
