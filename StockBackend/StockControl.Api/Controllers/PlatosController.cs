using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Recetas;
using StockControl.Domain.Common;

namespace StockControl.Api.Controllers;

/// <summary>Costeo de menú (Fase 3). Información de gerencia: no visible para digitadores.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
public class PlatosController(IRecetaService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PlatoDto>>> Listar([FromQuery] bool soloActivos = true) =>
        await service.ListarAsync(soloActivos);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PlatoDto>> Obtener(int id)
    {
        var plato = await service.ObtenerAsync(id);
        return plato is null ? NotFound() : plato;
    }

    [HttpPost]
    public async Task<ActionResult<PlatoDto>> Crear(CrearPlatoRequest req)
    {
        var plato = await service.CrearAsync(req);
        return CreatedAtAction(nameof(Obtener), new { id = plato.Id }, plato);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PlatoDto>> Actualizar(int id, ActualizarPlatoRequest req)
    {
        var plato = await service.ActualizarAsync(id, req);
        return plato is null ? NotFound() : plato;
    }

    /// <summary>Agrega o actualiza (por producto) un ingrediente de la receta.</summary>
    [HttpPost("{id:int}/ingredientes")]
    public async Task<ActionResult<PlatoDto>> UpsertIngrediente(int id, UpsertIngredienteRequest req)
    {
        var plato = await service.UpsertIngredienteAsync(id, req);
        return plato is null ? NotFound() : plato;
    }

    [HttpDelete("{id:int}/ingredientes/{ingredienteId:int}")]
    public async Task<ActionResult<PlatoDto>> EliminarIngrediente(int id, int ingredienteId)
    {
        var plato = await service.EliminarIngredienteAsync(id, ingredienteId);
        return plato is null ? NotFound() : plato;
    }

    /// <summary>Qué platos usan un producto y cuánto pesa en su costo (para anticipar subidas).</summary>
    [HttpGet("impacto/{productoId:int}")]
    public async Task<ActionResult<List<ImpactoPlatoDto>>> Impacto(int productoId) =>
        await service.ImpactoProductoAsync(productoId);
}
