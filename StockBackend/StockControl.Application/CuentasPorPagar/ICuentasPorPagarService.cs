namespace StockControl.Application.CuentasPorPagar;

public interface ICuentasPorPagarService
{
    Task<CuentasPorPagarResultadoDto> ListarAsync(FiltroCuentasPorPagar filtro, CancellationToken ct = default);
    Task<PagoProveedorDto> RegistrarPagoAsync(RegistrarPagoProveedorRequest req, CancellationToken ct = default);
    Task<bool> EliminarPagoAsync(int id, CancellationToken ct = default);
}
