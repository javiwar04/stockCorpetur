using Microsoft.EntityFrameworkCore;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;

namespace StockControl.Application.Auditoria;

public class AuditoriaService(IApplicationDbContext db, ICurrentUser currentUser) : IAuditoriaService
{
    public async Task<List<AuditoriaEventoDto>> ListarAsync(FiltroAuditoria filtro, CancellationToken ct = default)
    {
        if (!currentUser.EsAdmin && !currentUser.EsGerencia)
            throw new UnauthorizedAccessException("Solo Admin o Gerencia pueden consultar la auditoria.");

        if (filtro.HotelId is { } hotelId && !currentUser.PuedeAccederHotel(hotelId))
            throw new UnauthorizedAccessException("No tienes acceso a ese hotel.");

        var query = db.AuditoriaEventos
            .Include(a => a.Hotel)
            .AsQueryable();

        if (filtro.HotelId is not null) query = query.Where(a => a.HotelId == filtro.HotelId);
        if (!string.IsNullOrWhiteSpace(filtro.Accion)) query = query.Where(a => a.Accion == filtro.Accion.Trim());
        if (!string.IsNullOrWhiteSpace(filtro.Entidad)) query = query.Where(a => a.Entidad == filtro.Entidad.Trim());
        if (filtro.Desde is not null) query = query.Where(a => a.Fecha >= filtro.Desde.Value.ToDateTime(TimeOnly.MinValue));
        if (filtro.Hasta is not null) query = query.Where(a => a.Fecha < filtro.Hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var eventos = await query
            .OrderByDescending(a => a.Fecha)
            .ThenByDescending(a => a.Id)
            .Take(300)
            .ToListAsync(ct);

        return eventos.Select(Mapear).ToList();
    }

    public async Task<AuditoriaEventoDto> RegistrarAsync(RegistrarAuditoriaRequest req, CancellationToken ct = default)
    {
        var evento = new AuditoriaEvento
        {
            Fecha = DateTime.UtcNow,
            Usuario = currentUser.UserName ?? "sistema",
            Accion = Limitar(req.Accion, 80),
            Entidad = Limitar(req.Entidad, 80),
            EntidadId = req.EntidadId,
            HotelId = req.HotelId,
            Resumen = Limitar(req.Resumen, 300),
            Detalle = string.IsNullOrWhiteSpace(req.Detalle) ? null : Limitar(req.Detalle.Trim(), 1000),
        };

        db.AuditoriaEventos.Add(evento);
        await db.SaveChangesAsync(ct);

        if (evento.HotelId is not null)
            evento.Hotel = await db.Hoteles.FirstOrDefaultAsync(h => h.Id == evento.HotelId, ct);

        return Mapear(evento);
    }

    private static AuditoriaEventoDto Mapear(AuditoriaEvento e) => new(
        e.Id,
        e.Fecha,
        e.Usuario,
        e.Accion,
        e.Entidad,
        e.EntidadId,
        e.HotelId,
        e.Hotel?.Nombre,
        e.Resumen,
        e.Detalle);

    private static string Limitar(string valor, int max)
    {
        var limpio = string.IsNullOrWhiteSpace(valor) ? "-" : valor.Trim();
        return limpio.Length <= max ? limpio : limpio[..max];
    }
}
