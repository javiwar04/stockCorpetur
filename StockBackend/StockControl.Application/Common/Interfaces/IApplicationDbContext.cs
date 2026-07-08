using Microsoft.EntityFrameworkCore;
using StockControl.Domain.Entities;

namespace StockControl.Application.Common.Interfaces;

/// <summary>
/// Abstracción del contexto de datos expuesta a la capa de aplicación.
/// Solo expone las entidades de dominio (no las de Identity).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Hotel> Hoteles { get; }
    DbSet<Proveedor> Proveedores { get; }
    DbSet<UnidadMedida> Unidades { get; }
    DbSet<Producto> Productos { get; }
    DbSet<ConversionProducto> Conversiones { get; }
    DbSet<DocumentoCompra> Documentos { get; }
    DbSet<DetalleCompra> Detalles { get; }
    DbSet<PagoProveedor> PagosProveedor { get; }
    DbSet<ComensalMensual> Comensales { get; }
    DbSet<PresupuestoMensual> Presupuestos { get; }
    DbSet<MovimientoInventario> Movimientos { get; }
    DbSet<StockMinimo> StockMinimos { get; }
    DbSet<ConteoInventario> ConteosInventario { get; }
    DbSet<ConteoInventarioDetalle> ConteosInventarioDetalle { get; }
    DbSet<CierreMensual> CierresMensuales { get; }
    DbSet<AuditoriaEvento> AuditoriaEventos { get; }
    DbSet<Plato> Platos { get; }
    DbSet<RecetaDetalle> Recetas { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
