namespace StockControl.Application.Recetas;

public record IngredienteDto(
    int Id,
    int ProductoId,
    string Producto,
    string UnidadBase,
    decimal CantidadPorPorcion,
    decimal PrecioUnitario,
    decimal CostoLinea,
    bool TienePrecio);

/// <summary>
/// Plato con su costo calculado a precios recientes de compra.
/// FoodCostPorcentaje = costo / precio de venta (estándar del rubro: sano ≤ 30%).
/// </summary>
public record PlatoDto(
    int Id,
    string Nombre,
    decimal? PrecioVenta,
    bool Activo,
    decimal Costo,
    bool CostoCompleto,
    decimal? Margen,
    decimal? FoodCostPorcentaje,
    List<IngredienteDto> Ingredientes);

public record CrearPlatoRequest(string Nombre, decimal? PrecioVenta);

public record ActualizarPlatoRequest(string Nombre, decimal? PrecioVenta, bool Activo);

public record UpsertIngredienteRequest(int ProductoId, decimal CantidadPorPorcion);

/// <summary>Impacto de un producto en el menú: qué platos lo usan y cuánto pesa en su costo.</summary>
public record ImpactoPlatoDto(
    int PlatoId,
    string Plato,
    decimal CantidadPorPorcion,
    decimal CostoLinea,
    decimal CostoPlato,
    decimal PorcentajeDelCosto);
