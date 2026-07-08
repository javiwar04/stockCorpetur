using StockControl.Domain.Common;

namespace StockControl.Domain.Entities;

/// <summary>Uno de los 5 hoteles del grupo (Casona del Lago, El Mesón, etc.).</summary>
public class Hotel : EntidadBase
{
    public string Nombre { get; set; } = null!;
    public bool Activo { get; set; } = true;

    public ICollection<DocumentoCompra> Documentos { get; set; } = new List<DocumentoCompra>();
    public ICollection<ComensalMensual> Comensales { get; set; } = new List<ComensalMensual>();
    public ICollection<PresupuestoMensual> Presupuestos { get; set; } = new List<PresupuestoMensual>();
    public ICollection<StockMinimo> StockMinimos { get; set; } = new List<StockMinimo>();
    public ICollection<ConteoInventario> ConteosInventario { get; set; } = new List<ConteoInventario>();
    public ICollection<CierreMensual> CierresMensuales { get; set; } = new List<CierreMensual>();
}
