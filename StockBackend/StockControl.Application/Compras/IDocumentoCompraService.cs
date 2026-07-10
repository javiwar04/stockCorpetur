namespace StockControl.Application.Compras;

public record FiltroDocumentos(int? HotelId, int? ProveedorId, string? TipoCompra, DateOnly? Desde, DateOnly? Hasta);

public interface IDocumentoCompraService
{
    Task<List<DocumentoCompraResumenDto>> ListarAsync(FiltroDocumentos filtro, CancellationToken ct = default);
    Task<DocumentoCompraDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<DocumentoCompraDto> CrearAsync(CrearDocumentoCompraRequest req, CancellationToken ct = default);
    Task<DocumentoCompraDto?> ActualizarAsync(int id, CrearDocumentoCompraRequest req, CancellationToken ct = default);
    Task<DocumentoCompraDto?> RecibirAsync(int id, CancellationToken ct = default);
    Task<DocumentoCompraDto?> AnularAsync(int id, CancellationToken ct = default);
    Task<bool> EliminarAsync(int id, CancellationToken ct = default);
}
