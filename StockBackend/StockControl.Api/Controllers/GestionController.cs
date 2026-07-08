using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Gestion;
using StockControl.Domain.Common;

namespace StockControl.Api.Controllers;

/// <summary>Comensales mensuales y presupuestos por hotel (solo Admin/Gerencia).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
public class GestionController(IGestionService service) : ControllerBase
{
    [HttpGet("comensales")]
    public async Task<ActionResult<List<ComensalDto>>> Comensales([FromQuery] int anio, [FromQuery] int mes) =>
        await service.ListarComensalesAsync(anio, mes);

    [HttpPut("comensales")]
    public async Task<IActionResult> UpsertComensal(UpsertComensalRequest req)
    {
        await service.UpsertComensalAsync(req);
        return NoContent();
    }

    [HttpGet("presupuestos")]
    public async Task<ActionResult<List<PresupuestoDto>>> Presupuestos([FromQuery] int anio, [FromQuery] int mes) =>
        await service.ListarPresupuestosAsync(anio, mes);

    [HttpPut("presupuestos")]
    public async Task<IActionResult> UpsertPresupuesto(UpsertPresupuestoRequest req)
    {
        await service.UpsertPresupuestoAsync(req);
        return NoContent();
    }
}
