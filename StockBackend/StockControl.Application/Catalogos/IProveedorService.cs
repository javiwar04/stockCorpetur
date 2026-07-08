namespace StockControl.Application.Catalogos;

public interface IProveedorService
{
    Task<List<ProveedorDto>> ListarAsync(bool soloActivos, CancellationToken ct = default);
    Task<ProveedorDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<ProveedorDto> CrearAsync(CrearProveedorRequest req, CancellationToken ct = default);
    Task<ProveedorDto?> ActualizarAsync(int id, ActualizarProveedorRequest req, CancellationToken ct = default);
}
