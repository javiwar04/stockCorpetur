using StockControl.Domain.Common;
using StockControl.Domain.Enums;

namespace StockControl.Domain.Entities;

/// <summary>
/// Presupuesto de compra de un hotel para una categoría en un mes dado.
/// Habilita el comparativo gasto real vs. presupuesto y la proyección al cierre.
/// </summary>
public class PresupuestoMensual : EntidadBase
{
    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public CategoriaProducto Categoria { get; set; }

    public int Anio { get; set; }
    public int Mes { get; set; }

    public decimal Monto { get; set; }
}
