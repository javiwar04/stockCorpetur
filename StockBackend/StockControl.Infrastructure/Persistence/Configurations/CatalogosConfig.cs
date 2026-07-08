using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockControl.Domain.Entities;

namespace StockControl.Infrastructure.Persistence.Configurations;

public class HotelConfig : IEntityTypeConfiguration<Hotel>
{
    public void Configure(EntityTypeBuilder<Hotel> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class ProveedorConfig : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(160).IsRequired();
        b.Property(x => x.Nit).HasMaxLength(20);
        b.Property(x => x.Telefono).HasMaxLength(30);
        b.Property(x => x.DiasCredito).HasDefaultValue(0);
        b.HasIndex(x => x.Nombre);
    }
}

public class UnidadMedidaConfig : IEntityTypeConfiguration<UnidadMedida>
{
    public void Configure(EntityTypeBuilder<UnidadMedida> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(40).IsRequired();
        b.Property(x => x.Abreviatura).HasMaxLength(10).IsRequired();
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class ProductoConfig : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.Nombre).IsUnique();

        b.HasOne(x => x.UnidadBase)
            .WithMany()
            .HasForeignKey(x => x.UnidadBaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConversionProductoConfig : IEntityTypeConfiguration<ConversionProducto>
{
    public void Configure(EntityTypeBuilder<ConversionProducto> b)
    {
        b.Property(x => x.FactorABase).HasPrecision(18, 4);
        b.HasIndex(x => new { x.ProductoId, x.UnidadId }).IsUnique();

        b.HasOne(x => x.Producto)
            .WithMany(p => p.Conversiones)
            .HasForeignKey(x => x.ProductoId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Unidad)
            .WithMany()
            .HasForeignKey(x => x.UnidadId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
