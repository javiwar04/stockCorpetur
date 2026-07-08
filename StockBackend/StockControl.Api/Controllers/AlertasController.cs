using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Alertas;

namespace StockControl.Api.Controllers;

[ApiController]
[Route("api/alertas")]
[Authorize]
public class AlertasController(IAlertaService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AlertasResultadoDto>> Listar() =>
        await service.ListarAsync();

    [HttpGet("resumen")]
    public async Task<ActionResult<AlertasResumenDto>> Resumen() =>
        await service.ResumenAsync();
}
