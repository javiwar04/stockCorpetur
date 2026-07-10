namespace StockControl.Application.Compras;

public record DetalleCompraDto(
    int Id, int ProductoId, string ProductoNombre, int UnidadId, string UnidadNombre,
    decimal Cantidad, decimal PrecioUnitario, decimal Total);

public record DocumentoCompraDto(
    int Id, DateOnly Fecha, string NumeroDocumento,
    int HotelId, string HotelNombre, int ProveedorId, string ProveedorNombre,
    string Estado, decimal Retencion, string? Observaciones, decimal Total,
    List<DetalleCompraDto> Detalles);

public record DocumentoCompraResumenDto(
    int Id, DateOnly Fecha, string NumeroDocumento,
    int HotelId, string HotelNombre, int ProveedorId, string ProveedorNombre,
    string Estado, decimal Total);

public record CrearDetalleCompraRequest(int ProductoId, int UnidadId, decimal Cantidad, decimal PrecioUnitario);

public record CrearDocumentoCompraRequest(
    DateOnly Fecha,
    string NumeroDocumento,
    int HotelId,
    int ProveedorId,
    decimal Retencion,
    string? Observaciones,
    List<CrearDetalleCompraRequest> Detalles,
    string? Estado = null);
