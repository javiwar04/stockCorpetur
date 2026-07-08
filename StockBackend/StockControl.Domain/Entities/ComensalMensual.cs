using StockControl.Domain.Common;

namespace StockControl.Domain.Entities;

/// <summary>
/// Número de comensales/cubiertos de un hotel en un mes. Habilita el cálculo de
/// costo por comensal (food cost) a grano mensual.
/// </summary>
public class ComensalMensual : EntidadBase
{
    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public int Anio { get; set; }
    public int Mes { get; set; }

    public int NumeroComensales { get; set; }
}
