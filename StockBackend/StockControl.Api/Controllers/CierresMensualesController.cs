using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Cierres;
using StockControl.Domain.Common;

namespace StockControl.Api.Controllers;

[ApiController]
[Route("api/cierres-mensuales")]
[Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
public class CierresMensualesController(ICierreMensualService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CierreMensualDto>>> Listar(
        [FromQuery] int? hotelId,
        [FromQuery] int? anio,
        [FromQuery] int? mes) =>
        await service.ListarAsync(new FiltroCierresMensuales(hotelId, anio, mes));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CierreMensualDto>> Obtener(int id)
    {
        var cierre = await service.ObtenerAsync(id);
        return cierre is null ? NotFound() : cierre;
    }

    [HttpGet("preview")]
    public async Task<ActionResult<CierreMensualDto>> Preview(
        [FromQuery] int hotelId,
        [FromQuery] int anio,
        [FromQuery] int mes) =>
        await service.PreviewAsync(hotelId, anio, mes);

    [HttpPost]
    public async Task<ActionResult<CierreMensualDto>> Cerrar(CerrarMesRequest req)
    {
        var cierre = await service.CerrarAsync(req);
        return CreatedAtAction(nameof(Obtener), new { id = cierre.Id }, cierre);
    }

    [HttpPost("{id:int}/anular")]
    public async Task<ActionResult<CierreMensualDto>> Anular(int id, AnularCierreMensualRequest req)
    {
        var cierre = await service.AnularAsync(id, req);
        return cierre is null ? NotFound() : cierre;
    }
}
