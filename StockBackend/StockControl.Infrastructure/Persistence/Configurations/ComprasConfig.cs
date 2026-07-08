using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockControl.Domain.Entities;

namespace StockControl.Infrastructure.Persistence.Configurations;

public class DocumentoCompraConfig : IEntityTypeConfiguration<DocumentoCompra>
{
    public void Configure(EntityTypeBuilder<DocumentoCompra> b)
    {
        b.Property(x => x.NumeroDocumento).HasMaxLength(60).IsRequired();
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Retencion).HasPrecision(18, 2);
        b.Property(x => x.Observaciones).HasMaxLength(500);

        // El total se calcula desde los detalles: no se persiste.
        b.Ignore(x => x.Total);

        // Un mismo número de documento no se repite dentro del mismo hotel.
        b.HasIndex(x => new { x.HotelId, x.NumeroDocumento }).IsUnique();
        b.HasIndex(x => x.Fecha);

        b.HasOne(x => x.Hotel)
            .WithMany(h => h.Documentos)
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Proveedor)
            .WithMany(p => p.Documentos)
            .HasForeignKey(x => x.ProveedorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DetalleCompraConfig : IEntityTypeConfiguration<DetalleCompra>
{
    public void Configure(EntityTypeBuilder<DetalleCompra> b)
    {
        b.Property(x => x.Cantidad).HasPrecision(18, 4);
        b.Property(x => x.PrecioUnitario).HasPrecision(18, 4);
        b.Property(x => x.FactorABase).HasPrecision(18, 4);

        // Propiedades derivadas: no se persisten.
        b.Ignore(x => x.Total);
        b.Ignore(x => x.CantidadBase);
        b.Ignore(x => x.PrecioPorUnidadBase);

        b.HasOne(x => x.DocumentoCompra)
            .WithMany(d => d.Detalles)
            .HasForeignKey(x => x.DocumentoCompraId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Producto)
            .WithMany(p => p.Detalles)
            .HasForeignKey(x => x.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Unidad)
            .WithMany()
            .HasForeignKey(x => x.UnidadId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.ProductoId);
    }
}

public class PagoProveedorConfig : IEntityTypeConfiguration<PagoProveedor>
{
    public void Configure(EntityTypeBuilder<PagoProveedor> b)
    {
        b.ToTable("PagosProveedor");
        b.Property(x => x.Monto).HasPrecision(18, 2);
        b.Property(x => x.MetodoPago).HasMaxLength(50).IsRequired();
        b.Property(x => x.Referencia).HasMaxLength(120);
        b.Property(x => x.Observaciones).HasMaxLength(500);
        b.HasIndex(x => x.Fecha);
        b.HasIndex(x => x.ProveedorId);
        b.HasIndex(x => x.DocumentoCompraId);

        b.HasOne(x => x.DocumentoCompra)
            .WithMany(d => d.Pagos)
            .HasForeignKey(x => x.DocumentoCompraId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Proveedor)
            .WithMany(p => p.Pagos)
            .HasForeignKey(x => x.ProveedorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
