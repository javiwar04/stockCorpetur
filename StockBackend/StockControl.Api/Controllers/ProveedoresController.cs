using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Catalogos;
using StockControl.Domain.Common;

namespace StockControl.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProveedoresController(IProveedorService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProveedorDto>>> Listar([FromQuery] bool soloActivos = true) =>
        await service.ListarAsync(soloActivos);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProveedorDto>> Obtener(int id)
    {
        var proveedor = await service.ObtenerAsync(id);
        return proveedor is null ? NotFound() : proveedor;
    }

    [HttpPost]
    [Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
    public async Task<ActionResult<ProveedorDto>> Crear(CrearProveedorRequest req)
    {
        var proveedor = await service.CrearAsync(req);
        return CreatedAtAction(nameof(Obtener), new { id = proveedor.Id }, proveedor);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
    public async Task<ActionResult<ProveedorDto>> Actualizar(int id, ActualizarProveedorRequest req)
    {
        var proveedor = await service.ActualizarAsync(id, req);
        return proveedor is null ? NotFound() : proveedor;
    }
}
