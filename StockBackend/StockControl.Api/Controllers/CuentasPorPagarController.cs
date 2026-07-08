using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.CuentasPorPagar;

namespace StockControl.Api.Controllers;

[ApiController]
[Route("api/cuentas-por-pagar")]
[Authorize(Roles = $"{Domain.Common.RolesApp.Admin},{Domain.Common.RolesApp.Gerencia}")]
public class CuentasPorPagarController(ICuentasPorPagarService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CuentasPorPagarResultadoDto>> Listar(
        [FromQuery] int? hotelId,
        [FromQuery] int? proveedorId,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] bool soloPendientes = true) =>
        await service.ListarAsync(new FiltroCuentasPorPagar(hotelId, proveedorId, desde, hasta, soloPendientes));

    [HttpPost("pagos")]
    public async Task<ActionResult<PagoProveedorDto>> RegistrarPago(RegistrarPagoProveedorRequest req) =>
        await service.RegistrarPagoAsync(req);

    [HttpDelete("pagos/{id:int}")]
    public async Task<IActionResult> EliminarPago(int id) =>
        await service.EliminarPagoAsync(id) ? NoContent() : NotFound();
}
