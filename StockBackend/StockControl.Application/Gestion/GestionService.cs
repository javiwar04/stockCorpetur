using Microsoft.EntityFrameworkCore;
using StockControl.Application.Common.Interfaces;
using StockControl.Domain.Entities;
using StockControl.Domain.Enums;

namespace StockControl.Application.Gestion;

public interface IGestionService
{
    Task<List<ComensalDto>> ListarComensalesAsync(int anio, int mes, CancellationToken ct = default);
    Task UpsertComensalAsync(UpsertComensalRequest req, CancellationToken ct = default);
    Task<List<PresupuestoDto>> ListarPresupuestosAsync(int anio, int mes, CancellationToken ct = default);
    Task UpsertPresupuestoAsync(UpsertPresupuestoRequest req, CancellationToken ct = default);
}

/// <summary>Comensales mensuales y presupuestos por hotel: la base del food cost y del control presupuestario.</summary>
public class GestionService(IApplicationDbContext db) : IGestionService
{
    public async Task<List<ComensalDto>> ListarComensalesAsync(int anio, int mes, CancellationToken ct = default) =>
        (await db.Comensales
            .Include(c => c.Hotel)
            .Where(c => c.Anio == anio && c.Mes == mes)
            .ToListAsync(ct))
        .Select(c => new ComensalDto(c.HotelId, c.Hotel.Nombre, c.Anio, c.Mes, c.NumeroComensales))
        .OrderBy(c => c.Hotel)
        .ToList();

    public async Task UpsertComensalAsync(UpsertComensalRequest req, CancellationToken ct = default)
    {
        ValidarPeriodo(req.Anio, req.Mes);
        if (req.NumeroComensales < 0)
            throw new InvalidOperationException("El número de comensales no puede ser negativo.");

        var existente = await db.Comensales.FirstOrDefaultAsync(
            c => c.HotelId == req.HotelId && c.Anio == req.Anio && c.Mes == req.Mes, ct);

        if (existente is null)
            db.Comensales.Add(new ComensalMensual
            {
                HotelId = req.HotelId,
                Anio = req.Anio,
                Mes = req.Mes,
                NumeroComensales = req.NumeroComensales,
            });
        else
            existente.NumeroComensales = req.NumeroComensales;

        await db.SaveChangesAsync(ct);
    }

    public async Task<List<PresupuestoDto>> ListarPresupuestosAsync(int anio, int mes, CancellationToken ct = default) =>
        (await db.Presupuestos
            .Include(p => p.Hotel)
            .Where(p => p.Anio == anio && p.Mes == mes)
            .ToListAsync(ct))
        .Select(p => new PresupuestoDto(p.HotelId, p.Hotel.Nombre, p.Categoria.ToString(), p.Anio, p.Mes, p.Monto))
        .OrderBy(p => p.Hotel).ThenBy(p => p.Categoria)
        .ToList();

    public async Task UpsertPresupuestoAsync(UpsertPresupuestoRequest req, CancellationToken ct = default)
    {
        ValidarPeriodo(req.Anio, req.Mes);
        if (req.Monto < 0)
            throw new InvalidOperationException("El presupuesto no puede ser negativo.");
        if (!Enum.TryParse<CategoriaProducto>(req.Categoria, ignoreCase: true, out var categoria))
            throw new InvalidOperationException($"Categoría inválida: {req.Categoria}");

        var existente = await db.Presupuestos.FirstOrDefaultAsync(
            p => p.HotelId == req.HotelId && p.Categoria == categoria && p.Anio == req.Anio && p.Mes == req.Mes, ct);

        if (existente is null)
            db.Presupuestos.Add(new PresupuestoMensual
            {
                HotelId = req.HotelId,
                Categoria = categoria,
                Anio = req.Anio,
                Mes = req.Mes,
                Monto = req.Monto,
            });
        else
            existente.Monto = req.Monto;

        await db.SaveChangesAsync(ct);
    }

    private static void ValidarPeriodo(int anio, int mes)
    {
        if (mes is < 1 or > 12) throw new InvalidOperationException("Mes inválido.");
        if (anio is < 2020 or > 2100) throw new InvalidOperationException("Año inválido.");
    }
}
