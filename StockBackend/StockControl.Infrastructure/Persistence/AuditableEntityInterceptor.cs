using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Common;

namespace StockControl.Infrastructure.Persistence;

/// <summary>
/// Rellena automáticamente los campos de auditoría (CreadoEn/Por, ModificadoEn/Por)
/// de toda entidad que herede de <see cref="EntidadBase"/> al guardar.
/// </summary>
public class AuditableEntityInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Aplicar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Aplicar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void Aplicar(DbContext? context)
    {
        if (context is null) return;

        var ahora = DateTime.UtcNow;
        var usuario = currentUser.UserName ?? "sistema";

        foreach (var entry in context.ChangeTracker.Entries<EntidadBase>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreadoEn = ahora;
                entry.Entity.CreadoPor = usuario;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModificadoEn = ahora;
                entry.Entity.ModificadoPor = usuario;
            }
        }
    }
}
