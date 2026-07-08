using StockControl.Domain.Common;

namespace StockControl.Domain.Entities;

/// <summary>Evento de negocio visible para trazabilidad gerencial.</summary>
public class AuditoriaEvento : EntidadBase
{
    public DateTime Fecha { get; set; }
    public string Usuario { get; set; } = "sistema";
    public string Accion { get; set; } = null!;
    public string Entidad { get; set; } = null!;
    public int? EntidadId { get; set; }

    public int? HotelId { get; set; }
    public Hotel? Hotel { get; set; }

    public string Resumen { get; set; } = null!;
    public string? Detalle { get; set; }
}
