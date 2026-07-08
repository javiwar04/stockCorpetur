using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Auditoria;
using StockControl.Domain.Common;

namespace StockControl.Api.Controllers;

[ApiController]
[Route("api/auditoria")]
[Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
public class AuditoriaController(IAuditoriaService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AuditoriaEventoDto>>> Listar(
        [FromQuery] int? hotelId,
        [FromQuery] string? accion,
        [FromQuery] string? entidad,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta) =>
        await service.ListarAsync(new FiltroAuditoria(hotelId, accion, entidad, desde, hasta));
}
