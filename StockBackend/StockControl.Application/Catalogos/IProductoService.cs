namespace StockControl.Application.Catalogos;

public interface IProductoService
{
    Task<List<ProductoDto>> ListarAsync(bool soloActivos, CancellationToken ct = default);
    Task<ProductoDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<ProductoDto> CrearAsync(CrearProductoRequest req, CancellationToken ct = default);
    Task<ProductoDto?> ActualizarAsync(int id, ActualizarProductoRequest req, CancellationToken ct = default);

    Task<List<ConversionDto>> ListarConversionesAsync(int productoId, CancellationToken ct = default);
    Task<ConversionDto> AgregarConversionAsync(int productoId, CrearConversionRequest req, CancellationToken ct = default);
}
