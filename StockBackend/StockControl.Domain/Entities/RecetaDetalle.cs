using StockControl.Domain.Common;

namespace StockControl.Domain.Entities;

/// <summary>
/// FASE 3. Ingrediente de un <see cref="Plato"/>: qué producto y cuánto (en
/// unidad base) lleva una porción. Multiplicado por el precio vigente del
/// producto da el costo del plato.
/// </summary>
public class RecetaDetalle : EntidadBase
{
    public int PlatoId { get; set; }
    public Plato Plato { get; set; } = null!;

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    /// <summary>Cantidad de producto (en unidad base) por porción del plato.</summary>
    public decimal CantidadPorPorcion { get; set; }
}
