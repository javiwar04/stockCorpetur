namespace StockControl.Application.Dashboard;

/// <summary>Resumen del mes: gasto total, por hotel y por categoría, con comparativa al mes anterior.</summary>
public record ResumenMensualDto(
    int Anio,
    int Mes,
    decimal GastoTotal,
    decimal GastoMesAnterior,
    decimal VariacionPorcentaje,
    int DocumentosRegistrados,
    List<GastoPorHotelDto> PorHotel,
    List<GastoPorCategoriaDto> PorCategoria);

public record GastoPorHotelDto(
    int HotelId,
    string Hotel,
    decimal Gasto,
    int? Comensales,
    decimal? CostoPorComensal,
    decimal? Presupuesto,
    decimal? PorcentajePresupuesto);

public record GastoPorCategoriaDto(string Categoria, decimal Gasto);

/// <summary>Producto en el top: cantidad en unidad base y gasto acumulado.</summary>
public record TopProductoDto(
    int ProductoId,
    string Producto,
    string Categoria,
    string UnidadBase,
    decimal CantidadBase,
    decimal GastoTotal,
    decimal PrecioPromedioBase);

/// <summary>Punto de una serie mensual (tendencias de precio o consumo).</summary>
public record PuntoMensualDto(int Anio, int Mes, decimal Valor);

public record TendenciaPrecioDto(int ProductoId, string Producto, string UnidadBase, List<PuntoMensualDto> Serie);

public record ConsumoHotelSerieDto(int HotelId, string Hotel, List<PuntoMensualDto> Serie);

/// <summary>Alerta: el precio reciente de un producto supera su referencia histórica.</summary>
public record AlertaPrecioDto(
    int ProductoId,
    string Producto,
    string UnidadBase,
    decimal PrecioReciente,
    decimal PrecioReferencia,
    decimal IncrementoPorcentaje,
    DateOnly UltimaCompra);

public record DashboardGerencialDto(
    int Anio,
    int Mes,
    decimal ValorInventarioEstimado,
    int ProductosEnRiesgo,
    decimal ValorFaltanteEstimado,
    decimal ValorMermasEstimado,
    int MovimientosMerma,
    decimal ValorAjustesEstimado,
    int MovimientosAjuste,
    bool IncluyeFinanzas,
    decimal? SaldoCuentasPorPagar,
    decimal? SaldoCuentasVencido,
    int? DocumentosVencidos,
    List<TopProveedorSaldoDto> TopProveedoresSaldo,
    List<MermaProductoDto> TopMermas,
    List<StockCriticoDto> StockCritico);

public record TopProveedorSaldoDto(
    int ProveedorId,
    string Proveedor,
    int DocumentosPendientes,
    decimal Saldo,
    decimal SaldoVencido);

public record MermaProductoDto(
    int ProductoId,
    string Producto,
    string Categoria,
    string UnidadBase,
    decimal CantidadBase,
    decimal ValorEstimado);

public record StockCriticoDto(
    int HotelId,
    string Hotel,
    int ProductoId,
    string Producto,
    string Categoria,
    string UnidadBase,
    decimal Existencia,
    decimal StockMinimo,
    decimal Faltante,
    decimal ValorFaltanteEstimado,
    string EstadoStock);
