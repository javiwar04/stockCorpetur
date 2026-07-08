using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Catalogos;
using StockControl.Domain.Common;

namespace StockControl.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductosController(IProductoService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductoDto>>> Listar([FromQuery] bool soloActivos = true) =>
        await service.ListarAsync(soloActivos);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductoDto>> Obtener(int id)
    {
        var producto = await service.ObtenerAsync(id);
        return producto is null ? NotFound() : producto;
    }

    [HttpPost]
    [Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
    public async Task<ActionResult<ProductoDto>> Crear(CrearProductoRequest req)
    {
        var producto = await service.CrearAsync(req);
        return CreatedAtAction(nameof(Obtener), new { id = producto.Id }, producto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
    public async Task<ActionResult<ProductoDto>> Actualizar(int id, ActualizarProductoRequest req)
    {
        var producto = await service.ActualizarAsync(id, req);
        return producto is null ? NotFound() : producto;
    }

    [HttpGet("{id:int}/conversiones")]
    public async Task<ActionResult<List<ConversionDto>>> ListarConversiones(int id) =>
        await service.ListarConversionesAsync(id);

    [HttpPost("{id:int}/conversiones")]
    [Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
    public async Task<ActionResult<ConversionDto>> AgregarConversion(int id, CrearConversionRequest req) =>
        await service.AgregarConversionAsync(id, req);
}
