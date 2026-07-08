using StockControl.Domain.Common;

namespace StockControl.Domain.Entities;

/// <summary>
/// Línea de un <see cref="DocumentoCompra"/>: un producto, la unidad y cantidad
/// compradas y su precio unitario. Es la fuente de TODAS las métricas de Fase 1.
/// </summary>
public class DetalleCompra : EntidadBase
{
    public int DocumentoCompraId { get; set; }
    public DocumentoCompra DocumentoCompra { get; set; } = null!;

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    /// <summary>Unidad en la que se compró (puede diferir de la unidad base del producto).</summary>
    public int UnidadId { get; set; }
    public UnidadMedida Unidad { get; set; } = null!;

    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }

    /// <summary>
    /// Factor de conversión a unidad base aplicado al momento de registrar
    /// (copia de <see cref="ConversionProducto.FactorABase"/> para inmutabilidad histórica).
    /// </summary>
    public decimal FactorABase { get; set; } = 1m;

    public decimal Total => Cantidad * PrecioUnitario;

    /// <summary>Cantidad expresada en la unidad base del producto.</summary>
    public decimal CantidadBase => Cantidad * FactorABase;

    /// <summary>Precio por unidad base: la métrica comparable entre documentos y hoteles.</summary>
    public decimal PrecioPorUnidadBase => FactorABase == 0 ? 0 : PrecioUnitario / FactorABase;
}
