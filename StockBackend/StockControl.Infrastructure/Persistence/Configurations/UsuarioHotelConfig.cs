using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockControl.Infrastructure.Identity;

namespace StockControl.Infrastructure.Persistence.Configurations;

public class UsuarioHotelConfig : IEntityTypeConfiguration<UsuarioHotel>
{
    public void Configure(EntityTypeBuilder<UsuarioHotel> b)
    {
        b.HasKey(x => new { x.UsuarioId, x.HotelId });

        b.HasOne(x => x.Usuario)
            .WithMany(u => u.Hoteles)
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Hotel)
            .WithMany()
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ApplicationUserConfig : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
    }
}
