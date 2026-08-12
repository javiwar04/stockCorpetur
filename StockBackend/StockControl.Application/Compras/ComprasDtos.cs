namespace StockControl.Application.Compras;

public record DetalleCompraDto(
    int Id, int ProductoId, string ProductoNombre, int UnidadId, string UnidadNombre,
    int HotelId, string HotelNombre, decimal Cantidad, decimal PrecioUnitario, decimal Descuento, decimal Total);

public record DocumentoCompraDto(
    int Id, DateOnly Fecha, string NumeroDocumento, string NumeroPedido,
    int HotelId, string HotelNombre, int ProveedorId, string ProveedorNombre,
    string Estado, string TipoCompra, decimal Retencion, string? Observaciones, decimal Total,
    List<DetalleCompraDto> Detalles);

public record DocumentoCompraResumenDto(
    int Id, DateOnly Fecha, string NumeroDocumento, string NumeroPedido,
    int HotelId, string HotelNombre, int ProveedorId, string ProveedorNombre,
    string Estado, string TipoCompra, decimal Total);

public record CrearDetalleCompraRequest(
    int ProductoId,
    int UnidadId,
    decimal Cantidad,
    decimal PrecioUnitario,
    int? HotelId = null,
    decimal Descuento = 0);

public record CrearDocumentoCompraRequest(
    DateOnly Fecha,
    string NumeroDocumento,
    string NumeroPedido,
    int HotelId,
    int ProveedorId,
    decimal Retencion,
    string? Observaciones,
    List<CrearDetalleCompraRequest> Detalles,
    string? Estado = null,
    string? TipoCompra = null);
