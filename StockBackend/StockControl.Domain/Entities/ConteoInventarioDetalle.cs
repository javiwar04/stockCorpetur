using StockControl.Domain.Common;

namespace StockControl.Domain.Entities;

/// <summary>Producto contado dentro de un conteo fisico, con snapshot del sistema.</summary>
public class ConteoInventarioDetalle : EntidadBase
{
    public int ConteoInventarioId { get; set; }
    public ConteoInventario ConteoInventario { get; set; } = null!;

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public decimal CantidadSistemaBase { get; set; }
    public decimal CantidadFisicaBase { get; set; }
    public decimal DiferenciaBase { get; set; }
    public decimal ValorDiferenciaEstimado { get; set; }

    public int? MovimientoAjusteId { get; set; }
    public MovimientoInventario? MovimientoAjuste { get; set; }
}
