namespace StockControl.Application.Conteos;

public record FiltroConteos(int? HotelId, DateOnly? Desde, DateOnly? Hasta);

public record PlantillaConteoItemDto(
    int ProductoId,
    string Producto,
    string Categoria,
    string UnidadBase,
    decimal ExistenciaSistemaBase,
    decimal StockMinimoBase,
    decimal UltimoPrecioBase,
    decimal ValorSistemaEstimado);

public record ConteoInventarioResumenDto(
    int Id,
    DateOnly Fecha,
    int HotelId,
    string Hotel,
    string Estado,
    int ProductosContados,
    int ProductosConDiferencia,
    decimal ValorDiferenciaEstimado,
    string? Observaciones,
    DateTime CreadoEn,
    string? CreadoPor,
    DateTime? AjustesAplicadosEn,
    string? AjustesAplicadosPor);

public record ConteoInventarioDto(
    int Id,
    DateOnly Fecha,
    int HotelId,
    string Hotel,
    string Estado,
    string? Observaciones,
    DateTime CreadoEn,
    string? CreadoPor,
    DateTime? AjustesAplicadosEn,
    string? AjustesAplicadosPor,
    int ProductosContados,
    int ProductosConDiferencia,
    decimal ValorDiferenciaEstimado,
    List<ConteoInventarioDetalleDto> Detalles);

public record ConteoInventarioDetalleDto(
    int Id,
    int ProductoId,
    string Producto,
    string Categoria,
    string UnidadBase,
    decimal CantidadSistemaBase,
    decimal CantidadFisicaBase,
    decimal DiferenciaBase,
    decimal ValorDiferenciaEstimado,
    int? MovimientoAjusteId);

public record CrearConteoInventarioRequest(
    DateOnly Fecha,
    int HotelId,
    string? Observaciones,
    List<CrearConteoInventarioDetalleRequest> Detalles);

public record CrearConteoInventarioDetalleRequest(int ProductoId, decimal CantidadFisicaBase);
