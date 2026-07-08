using StockControl.Domain.Common;
using StockControl.Domain.Enums;

namespace StockControl.Domain.Entities;

/// <summary>Snapshot gerencial de inventario, compras y cuentas por pagar para un hotel/mes.</summary>
public class CierreMensual : EntidadBase
{
    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public int Anio { get; set; }
    public int Mes { get; set; }

    public EstadoCierreMensual Estado { get; set; } = EstadoCierreMensual.Cerrado;

    public decimal ComprasTotal { get; set; }
    public int DocumentosCompra { get; set; }

    public decimal ValorInventarioEstimado { get; set; }
    public int ProductosEnRiesgo { get; set; }
    public decimal ValorFaltanteEstimado { get; set; }

    public decimal ValorMermasEstimado { get; set; }
    public int MovimientosMerma { get; set; }

    public decimal ValorAjustesEstimado { get; set; }
    public int MovimientosAjuste { get; set; }

    public int ConteosFisicos { get; set; }
    public decimal ValorDiferenciasConteo { get; set; }

    public decimal SaldoCuentasPorPagar { get; set; }
    public decimal SaldoCuentasVencido { get; set; }
    public int DocumentosVencidos { get; set; }

    public DateTime FechaCierre { get; set; }
    public string? Observaciones { get; set; }
}
