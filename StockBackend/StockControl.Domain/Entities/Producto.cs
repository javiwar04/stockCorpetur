using StockControl.Domain.Common;
using StockControl.Domain.Enums;

namespace StockControl.Domain.Entities;

/// <summary>
/// Producto comprable (tomate, cebolla, fresa...). La <see cref="UnidadBaseId"/>
/// es la unidad canónica en la que se comparan precios y se acumula inventario,
/// de modo que compras hechas en cajas/mallas/libras sean comparables entre sí.
/// </summary>
public class Producto : EntidadBase
{
    public string Nombre { get; set; } = null!;
    public CategoriaProducto Categoria { get; set; }
    public bool Activo { get; set; } = true;

    /// <summary>Unidad canónica para análisis (p. ej. libra). Referencia a un <see cref="UnidadMedida"/>.</summary>
    public int UnidadBaseId { get; set; }
    public UnidadMedida UnidadBase { get; set; } = null!;

    public ICollection<ConversionProducto> Conversiones { get; set; } = new List<ConversionProducto>();
    public ICollection<DetalleCompra> Detalles { get; set; } = new List<DetalleCompra>();
    public ICollection<StockMinimo> StockMinimos { get; set; } = new List<StockMinimo>();
    public ICollection<ConteoInventarioDetalle> ConteosInventario { get; set; } = new List<ConteoInventarioDetalle>();
}
