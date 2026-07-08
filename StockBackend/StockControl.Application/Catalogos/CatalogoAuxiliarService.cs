using Microsoft.EntityFrameworkCore;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;

namespace StockControl.Application.Catalogos;

public class CatalogoAuxiliarService(IApplicationDbContext db) : ICatalogoAuxiliarService
{
    public async Task<List<UnidadDto>> ListarUnidadesAsync(CancellationToken ct = default) =>
        await db.Unidades.OrderBy(u => u.Nombre)
            .Select(u => new UnidadDto(u.Id, u.Nombre, u.Abreviatura))
            .ToListAsync(ct);

    public async Task<UnidadDto> CrearUnidadAsync(CrearUnidadRequest req, CancellationToken ct = default)
    {
        var unidad = new UnidadMedida { Nombre = req.Nombre.Trim(), Abreviatura = req.Abreviatura.Trim() };
        db.Unidades.Add(unidad);
        await db.SaveChangesAsync(ct);
        return new UnidadDto(unidad.Id, unidad.Nombre, unidad.Abreviatura);
    }

    public async Task<List<HotelDto>> ListarHotelesAsync(bool soloActivos, CancellationToken ct = default)
    {
        var query = db.Hoteles.AsQueryable();
        if (soloActivos) query = query.Where(h => h.Activo);
        return await query.OrderBy(h => h.Nombre)
            .Select(h => new HotelDto(h.Id, h.Nombre, h.Activo))
            .ToListAsync(ct);
    }
}
