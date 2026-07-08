using StockControl.Domain.Common;
using StockControl.Domain.Enums;

namespace StockControl.Domain.Entities;

/// <summary>
/// FASE 2. Movimiento de existencias de un producto en un hotel. Las compras
/// NO se duplican aquí: son entradas implícitas (la existencia se calcula como
/// compras − salidas − mermas ± ajustes). <c>Entrada</c> se reserva para
/// ingresos sin compra (stock inicial, traslados). Cantidad siempre en la
/// unidad base del producto; positiva salvo <c>Ajuste</c>, que lleva signo.
/// </summary>
public class MovimientoInventario : EntidadBase
{
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public TipoMovimiento Tipo { get; set; }
    public DateOnly Fecha { get; set; }

    /// <summary>
    /// Cantidad en unidad base. Positiva para Entrada/Salida/Merma (el efecto lo
    /// determina el <see cref="Tipo"/>); con signo para Ajuste (+ sobra, − falta).
    /// </summary>
    public decimal CantidadBase { get; set; }

    /// <summary>Reservado para trazabilidad futura (no se usa: las compras no se duplican como movimientos).</summary>
    public int? DocumentoCompraId { get; set; }
    public DocumentoCompra? DocumentoCompra { get; set; }

    public string? Referencia { get; set; }
}
