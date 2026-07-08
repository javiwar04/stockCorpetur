using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockControl.Domain.Entities;

namespace StockControl.Infrastructure.Persistence.Configurations;

public class ComensalMensualConfig : IEntityTypeConfiguration<ComensalMensual>
{
    public void Configure(EntityTypeBuilder<ComensalMensual> b)
    {
        b.HasIndex(x => new { x.HotelId, x.Anio, x.Mes }).IsUnique();
        b.HasOne(x => x.Hotel)
            .WithMany(h => h.Comensales)
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PresupuestoMensualConfig : IEntityTypeConfiguration<PresupuestoMensual>
{
    public void Configure(EntityTypeBuilder<PresupuestoMensual> b)
    {
        b.Property(x => x.Monto).HasPrecision(18, 2);
        b.HasIndex(x => new { x.HotelId, x.Categoria, x.Anio, x.Mes }).IsUnique();
        b.HasOne(x => x.Hotel)
            .WithMany(h => h.Presupuestos)
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MovimientoInventarioConfig : IEntityTypeConfiguration<MovimientoInventario>
{
    public void Configure(EntityTypeBuilder<MovimientoInventario> b)
    {
        b.Property(x => x.CantidadBase).HasPrecision(18, 4);
        b.Property(x => x.Referencia).HasMaxLength(120);
        b.HasIndex(x => new { x.HotelId, x.ProductoId, x.Fecha });

        b.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Hotel).WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DocumentoCompra).WithMany().HasForeignKey(x => x.DocumentoCompraId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class StockMinimoConfig : IEntityTypeConfiguration<StockMinimo>
{
    public void Configure(EntityTypeBuilder<StockMinimo> b)
    {
        b.Property(x => x.CantidadMinimaBase).HasPrecision(18, 4);
        b.HasIndex(x => new { x.HotelId, x.ProductoId }).IsUnique();

        b.HasOne(x => x.Hotel)
            .WithMany(h => h.StockMinimos)
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Producto)
            .WithMany(p => p.StockMinimos)
            .HasForeignKey(x => x.ProductoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConteoInventarioConfig : IEntityTypeConfiguration<ConteoInventario>
{
    public void Configure(EntityTypeBuilder<ConteoInventario> b)
    {
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Observaciones).HasMaxLength(500);
        b.Property(x => x.AjustesAplicadosPor).HasMaxLength(120);
        b.HasIndex(x => new { x.HotelId, x.Fecha });

        b.HasOne(x => x.Hotel)
            .WithMany(h => h.ConteosInventario)
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConteoInventarioDetalleConfig : IEntityTypeConfiguration<ConteoInventarioDetalle>
{
    public void Configure(EntityTypeBuilder<ConteoInventarioDetalle> b)
    {
        b.Property(x => x.CantidadSistemaBase).HasPrecision(18, 4);
        b.Property(x => x.CantidadFisicaBase).HasPrecision(18, 4);
        b.Property(x => x.DiferenciaBase).HasPrecision(18, 4);
        b.Property(x => x.ValorDiferenciaEstimado).HasPrecision(18, 2);
        b.HasIndex(x => new { x.ConteoInventarioId, x.ProductoId }).IsUnique();
        b.HasIndex(x => x.MovimientoAjusteId);

        b.HasOne(x => x.ConteoInventario)
            .WithMany(c => c.Detalles)
            .HasForeignKey(x => x.ConteoInventarioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Producto)
            .WithMany(p => p.ConteosInventario)
            .HasForeignKey(x => x.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.MovimientoAjuste)
            .WithMany()
            .HasForeignKey(x => x.MovimientoAjusteId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class CierreMensualConfig : IEntityTypeConfiguration<CierreMensual>
{
    public void Configure(EntityTypeBuilder<CierreMensual> b)
    {
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Observaciones).HasMaxLength(500);

        b.Property(x => x.ComprasTotal).HasPrecision(18, 2);
        b.Property(x => x.ValorInventarioEstimado).HasPrecision(18, 2);
        b.Property(x => x.ValorFaltanteEstimado).HasPrecision(18, 2);
        b.Property(x => x.ValorMermasEstimado).HasPrecision(18, 2);
        b.Property(x => x.ValorAjustesEstimado).HasPrecision(18, 2);
        b.Property(x => x.ValorDiferenciasConteo).HasPrecision(18, 2);
        b.Property(x => x.SaldoCuentasPorPagar).HasPrecision(18, 2);
        b.Property(x => x.SaldoCuentasVencido).HasPrecision(18, 2);

        b.HasIndex(x => new { x.HotelId, x.Anio, x.Mes })
            .IsUnique()
            .HasFilter("[Estado] = 'Cerrado'");

        b.HasOne(x => x.Hotel)
            .WithMany(h => h.CierresMensuales)
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AuditoriaEventoConfig : IEntityTypeConfiguration<AuditoriaEvento>
{
    public void Configure(EntityTypeBuilder<AuditoriaEvento> b)
    {
        b.ToTable("AuditoriaEventos");
        b.Property(x => x.Usuario).HasMaxLength(120).IsRequired();
        b.Property(x => x.Accion).HasMaxLength(80).IsRequired();
        b.Property(x => x.Entidad).HasMaxLength(80).IsRequired();
        b.Property(x => x.Resumen).HasMaxLength(300).IsRequired();
        b.Property(x => x.Detalle).HasMaxLength(1000);
        b.HasIndex(x => x.Fecha);
        b.HasIndex(x => new { x.HotelId, x.Fecha });
        b.HasIndex(x => new { x.Entidad, x.EntidadId });

        b.HasOne(x => x.Hotel)
            .WithMany()
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class PlatoConfig : IEntityTypeConfiguration<Plato>
{
    public void Configure(EntityTypeBuilder<Plato> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        b.Property(x => x.PrecioVenta).HasPrecision(18, 2);
    }
}

public class RecetaDetalleConfig : IEntityTypeConfiguration<RecetaDetalle>
{
    public void Configure(EntityTypeBuilder<RecetaDetalle> b)
    {
        b.Property(x => x.CantidadPorPorcion).HasPrecision(18, 4);
        b.HasIndex(x => new { x.PlatoId, x.ProductoId }).IsUnique();

        b.HasOne(x => x.Plato).WithMany(p => p.Ingredientes).HasForeignKey(x => x.PlatoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
    }
}
