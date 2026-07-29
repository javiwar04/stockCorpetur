namespace StockControl.Application.Dashboard;

public interface IDashboardService
{
    Task<ResumenMensualDto> ResumenMensualAsync(int anio, int mes, int? hotelId = null, CancellationToken ct = default);
    Task<List<TopProductoDto>> TopCompradosAsync(int meses, int top, int? hotelId = null, CancellationToken ct = default);
    Task<List<TopProductoDto>> TopCarosAsync(int meses, int top, int? hotelId = null, CancellationToken ct = default);
    Task<TendenciaPrecioDto?> TendenciaPrecioAsync(int productoId, int meses, int? hotelId = null, CancellationToken ct = default);
    Task<List<ConsumoHotelSerieDto>> ConsumoPorHotelAsync(int meses, int? hotelId = null, CancellationToken ct = default);
    Task<List<AlertaPrecioDto>> AlertasPrecioAsync(decimal umbralPorcentaje, int? hotelId = null, CancellationToken ct = default);
    Task<DashboardGerencialDto> GerencialAsync(int anio, int mes, int? hotelId = null, CancellationToken ct = default);
}
