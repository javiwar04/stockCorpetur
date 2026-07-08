using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Catalogos;
using StockControl.Domain.Common;

namespace StockControl.Api.Controllers;

/// <summary>Catálogos auxiliares de solo lectura para poblar formularios (unidades, hoteles).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CatalogosController(ICatalogoAuxiliarService service) : ControllerBase
{
    [HttpGet("unidades")]
    public async Task<ActionResult<List<UnidadDto>>> ListarUnidades() =>
        await service.ListarUnidadesAsync();

    [HttpPost("unidades")]
    [Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
    public async Task<ActionResult<UnidadDto>> CrearUnidad(CrearUnidadRequest req) =>
        await service.CrearUnidadAsync(req);

    [HttpGet("hoteles")]
    public async Task<ActionResult<List<HotelDto>>> ListarHoteles([FromQuery] bool soloActivos = true) =>
        await service.ListarHotelesAsync(soloActivos);
}
