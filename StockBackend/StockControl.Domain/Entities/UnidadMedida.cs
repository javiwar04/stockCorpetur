using StockControl.Domain.Common;

namespace StockControl.Domain.Entities;

/// <summary>
/// Unidad en la que se compra un producto (libra, unidad, caja, malla, manojo...).
/// Es un catálogo; la relación con cada producto y su factor de conversión
/// a la unidad base vive en <see cref="ConversionProducto"/>.
/// </summary>
public class UnidadMedida : EntidadBase
{
    public string Nombre { get; set; } = null!;
    public string Abreviatura { get; set; } = null!;
}
