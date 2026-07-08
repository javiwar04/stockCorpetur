using StockControl.Domain.Common;

namespace StockControl.Domain.Entities;

/// <summary>
/// Factor de conversión de una unidad de compra a la unidad base de un producto.
/// Ejemplo: producto "tomate" (base = libra), unidad "caja" con
/// <see cref="FactorABase"/> = 25 → 1 caja = 25 libras. Permite normalizar
/// cantidad y precio para que sean comparables entre documentos.
/// </summary>
public class ConversionProducto : EntidadBase
{
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public int UnidadId { get; set; }
    public UnidadMedida Unidad { get; set; } = null!;

    /// <summary>Cuántas unidades base equivale 1 unidad de compra. Debe ser &gt; 0.</summary>
    public decimal FactorABase { get; set; }
}
