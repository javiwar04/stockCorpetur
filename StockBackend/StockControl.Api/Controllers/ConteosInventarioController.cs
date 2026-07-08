using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Conteos;
using StockControl.Domain.Common;

namespace StockControl.Api.Controllers;

[ApiController]
[Route("api/conteos-inventario")]
[Authorize]
public class ConteosInventarioController(IConteoInventarioService service) : ControllerBase
{
    [HttpGet("plantilla/{hotelId:int}")]
    public async Task<ActionResult<List<PlantillaConteoItemDto>>> Plantilla(
        int hotelId,
        [FromQuery] DateOnly? fecha) =>
        await service.PlantillaAsync(hotelId, fecha);

    [HttpGet]
    public async Task<ActionResult<List<ConteoInventarioResumenDto>>> Listar(
        [FromQuery] int? hotelId,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta) =>
        await service.ListarAsync(new FiltroConteos(hotelId, desde, hasta));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ConteoInventarioDto>> Obtener(int id)
    {
        var conteo = await service.ObtenerAsync(id);
        return conteo is null ? NotFound() : conteo;
    }

    [HttpPost]
    public async Task<ActionResult<ConteoInventarioDto>> Crear(CrearConteoInventarioRequest req)
    {
        var conteo = await service.CrearAsync(req);
        return CreatedAtAction(nameof(Obtener), new { id = conteo.Id }, conteo);
    }

    [HttpPost("{id:int}/aplicar-ajustes")]
    [Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
    public async Task<ActionResult<ConteoInventarioDto>> AplicarAjustes(int id)
    {
        var conteo = await service.AplicarAjustesAsync(id);
        return conteo is null ? NotFound() : conteo;
    }
}
