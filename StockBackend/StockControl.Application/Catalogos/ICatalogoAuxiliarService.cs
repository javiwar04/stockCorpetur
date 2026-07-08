namespace StockControl.Application.Catalogos;

/// <summary>Catálogos auxiliares de solo lectura + alta de unidad (Hoteles/Unidades no tienen flujo de edición propio en Fase 1).</summary>
public interface ICatalogoAuxiliarService
{
    Task<List<UnidadDto>> ListarUnidadesAsync(CancellationToken ct = default);
    Task<UnidadDto> CrearUnidadAsync(CrearUnidadRequest req, CancellationToken ct = default);
    Task<List<HotelDto>> ListarHotelesAsync(bool soloActivos, CancellationToken ct = default);
}
