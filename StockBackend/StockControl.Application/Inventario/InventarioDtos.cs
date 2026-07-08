namespace StockControl.Application.Inventario;

/// <summary>Existencia calculada de un producto en un hotel (en unidad base).</summary>
public record ExistenciaDto(
    int ProductoId,
    string Producto,
    string Categoria,
    string UnidadBase,
    decimal Comprado,
    decimal Salidas,
    decimal Mermas,
    decimal Ajustes,
    decimal Existencia,
    decimal StockMinimo,
    decimal Faltante,
    string EstadoStock);

public record StockMinimoDto(
    int HotelId,
    int ProductoId,
    string Producto,
    string UnidadBase,
    decimal CantidadMinimaBase);

public record AlertaStockDto(
    int HotelId,
    string Hotel,
    int ProductoId,
    string Producto,
    string Categoria,
    string UnidadBase,
    decimal Existencia,
    decimal StockMinimo,
    decimal Faltante,
    string EstadoStock);

public record SugerenciaCompraDto(
    int HotelId,
    string Hotel,
    int ProductoId,
    string Producto,
    string Categoria,
    string UnidadBase,
    decimal CantidadSugeridaBase,
    decimal Existencia,
    decimal StockMinimo,
    decimal? UltimoPrecioBase,
    int? ProveedorId,
    string? ProveedorNombre,
    DateOnly? UltimaCompra,
    decimal? CostoEstimado);

public record KardexDto(
    int HotelId,
    string Hotel,
    int ProductoId,
    string Producto,
    string UnidadBase,
    DateOnly? Desde,
    DateOnly? Hasta,
    decimal SaldoInicial,
    decimal TotalEntradas,
    decimal TotalSalidas,
    decimal TotalAjustes,
    decimal SaldoFinal,
    List<KardexMovimientoDto> Movimientos);

public record KardexMovimientoDto(
    string Id,
    DateOnly Fecha,
    string Tipo,
    string Referencia,
    decimal Entrada,
    decimal Salida,
    decimal Ajuste,
    decimal Saldo,
    decimal? CostoUnitario,
    decimal? CostoTotal,
    string? Documento,
    string? Proveedor,
    string? CreadoPor);

public record MovimientoDto(
    int Id,
    string Tipo,
    DateOnly Fecha,
    int HotelId,
    string Hotel,
    int ProductoId,
    string Producto,
    string UnidadBase,
    decimal CantidadBase,
    string? Referencia,
    string? CreadoPor);

/// <summary>
/// Registro de un movimiento. La cantidad viene en la unidad elegida y se
/// convierte a unidad base con el factor configurado del producto.
/// </summary>
public record CrearMovimientoRequest(
    string Tipo,
    DateOnly Fecha,
    int HotelId,
    int ProductoId,
    int UnidadId,
    decimal Cantidad,
    string? Referencia);

public record FiltroMovimientos(int? HotelId, int? ProductoId, DateOnly? Desde, DateOnly? Hasta);

public record FiltroKardex(int HotelId, int ProductoId, DateOnly? Desde, DateOnly? Hasta);

public record GuardarStockMinimoRequest(int HotelId, int ProductoId, decimal CantidadMinimaBase);
