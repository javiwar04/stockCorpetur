using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;
using StockControl.Infrastructure.Identity;

namespace StockControl.Infrastructure.Persistence;

/// <summary>
/// Contexto de datos. Combina las tablas de Identity (usuarios, roles) con las
/// entidades de dominio, e implementa <see cref="IApplicationDbContext"/> para
/// que la capa de aplicación no dependa de Identity ni de EF directamente.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    public DbSet<Hotel> Hoteles => Set<Hotel>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<UnidadMedida> Unidades => Set<UnidadMedida>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ConversionProducto> Conversiones => Set<ConversionProducto>();
    public DbSet<DocumentoCompra> Documentos => Set<DocumentoCompra>();
    public DbSet<DetalleCompra> Detalles => Set<DetalleCompra>();
    public DbSet<PagoProveedor> PagosProveedor => Set<PagoProveedor>();
    public DbSet<ComensalMensual> Comensales => Set<ComensalMensual>();
    public DbSet<PresupuestoMensual> Presupuestos => Set<PresupuestoMensual>();
    public DbSet<MovimientoInventario> Movimientos => Set<MovimientoInventario>();
    public DbSet<StockMinimo> StockMinimos => Set<StockMinimo>();
    public DbSet<ConteoInventario> ConteosInventario => Set<ConteoInventario>();
    public DbSet<ConteoInventarioDetalle> ConteosInventarioDetalle => Set<ConteoInventarioDetalle>();
    public DbSet<CierreMensual> CierresMensuales => Set<CierreMensual>();
    public DbSet<AuditoriaEvento> AuditoriaEventos => Set<AuditoriaEvento>();
    public DbSet<Plato> Platos => Set<Plato>();
    public DbSet<RecetaDetalle> Recetas => Set<RecetaDetalle>();

    public DbSet<UsuarioHotel> UsuariosHoteles => Set<UsuarioHotel>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
