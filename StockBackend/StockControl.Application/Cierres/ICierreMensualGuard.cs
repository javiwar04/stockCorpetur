using Microsoft.EntityFrameworkCore;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Enums;

namespace StockControl.Application.Cierres;

public interface ICierreMensualGuard
{
    Task AsegurarPeriodoAbiertoAsync(int hotelId, DateOnly fecha, string accion, CancellationToken ct = default);
    Task<bool> EstaCerradoAsync(int hotelId, DateOnly fecha, CancellationToken ct = default);
}

public class CierreMensualGuard(IApplicationDbContext db) : ICierreMensualGuard
{
    public async Task AsegurarPeriodoAbiertoAsync(int hotelId, DateOnly fecha, string accion, CancellationToken ct = default)
    {
        if (!await EstaCerradoAsync(hotelId, fecha, ct)) return;

        throw new InvalidOperationException(
            $"No se puede {accion} en {fecha:yyyy-MM}: el mes ya tiene cierre mensual.");
    }

    public Task<bool> EstaCerradoAsync(int hotelId, DateOnly fecha, CancellationToken ct = default) =>
        db.CierresMensuales.AnyAsync(
            c => c.HotelId == hotelId
                 && c.Anio == fecha.Year
                 && c.Mes == fecha.Month
                 && c.Estado == EstadoCierreMensual.Cerrado,
            ct);
}
