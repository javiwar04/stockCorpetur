using Microsoft.EntityFrameworkCore;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;

namespace StockControl.Application.Catalogos;

public class ProveedorService(IApplicationDbContext db) : IProveedorService
{
    public async Task<List<ProveedorDto>> ListarAsync(bool soloActivos, CancellationToken ct = default)
    {
        var query = db.Proveedores.AsQueryable();
        if (soloActivos) query = query.Where(p => p.Activo);

        return await query.OrderBy(p => p.Nombre)
            .Select(p => new ProveedorDto(p.Id, p.Nombre, p.Nit, p.Telefono, p.DiasCredito, p.Activo))
            .ToListAsync(ct);
    }

    public async Task<ProveedorDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Proveedores.FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : new ProveedorDto(p.Id, p.Nombre, p.Nit, p.Telefono, p.DiasCredito, p.Activo);
    }

    public async Task<ProveedorDto> CrearAsync(CrearProveedorRequest req, CancellationToken ct = default)
    {
        if (req.DiasCredito < 0)
            throw new InvalidOperationException("Los dias de credito no pueden ser negativos.");

        var proveedor = new Proveedor { Nombre = req.Nombre.Trim(), Nit = req.Nit, Telefono = req.Telefono, DiasCredito = req.DiasCredito };
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync(ct);
        return new ProveedorDto(proveedor.Id, proveedor.Nombre, proveedor.Nit, proveedor.Telefono, proveedor.DiasCredito, proveedor.Activo);
    }

    public async Task<ProveedorDto?> ActualizarAsync(int id, ActualizarProveedorRequest req, CancellationToken ct = default)
    {
        var proveedor = await db.Proveedores.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (proveedor is null) return null;

        if (req.DiasCredito < 0)
            throw new InvalidOperationException("Los dias de credito no pueden ser negativos.");

        proveedor.Nombre = req.Nombre.Trim();
        proveedor.Nit = req.Nit;
        proveedor.Telefono = req.Telefono;
        proveedor.DiasCredito = req.DiasCredito;
        proveedor.Activo = req.Activo;

        await db.SaveChangesAsync(ct);
        return new ProveedorDto(proveedor.Id, proveedor.Nombre, proveedor.Nit, proveedor.Telefono, proveedor.DiasCredito, proveedor.Activo);
    }
}
