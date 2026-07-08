using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Compras;

namespace StockControl.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentosController(IDocumentoCompraService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<DocumentoCompraResumenDto>>> Listar(
        [FromQuery] int? hotelId, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta) =>
        await service.ListarAsync(new FiltroDocumentos(hotelId, desde, hasta));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DocumentoCompraDto>> Obtener(int id)
    {
        var documento = await service.ObtenerAsync(id);
        return documento is null ? NotFound() : documento;
    }

    [HttpPost]
    public async Task<ActionResult<DocumentoCompraDto>> Crear(CrearDocumentoCompraRequest req)
    {
        var documento = await service.CrearAsync(req);
        return CreatedAtAction(nameof(Obtener), new { id = documento.Id }, documento);
    }

    /// <summary>Reemplaza encabezado y líneas del documento (corrección de errores de digitación).</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<DocumentoCompraDto>> Actualizar(int id, CrearDocumentoCompraRequest req)
    {
        var documento = await service.ActualizarAsync(id, req);
        return documento is null ? NotFound() : documento;
    }

    [HttpPatch("{id:int}/recibir")]
    public async Task<ActionResult<DocumentoCompraDto>> Recibir(int id)
    {
        var documento = await service.RecibirAsync(id);
        return documento is null ? NotFound() : documento;
    }

    [HttpPatch("{id:int}/anular")]
    [Authorize(Roles = $"{Domain.Common.RolesApp.Admin},{Domain.Common.RolesApp.Gerencia}")]
    public async Task<ActionResult<DocumentoCompraDto>> Anular(int id)
    {
        var documento = await service.AnularAsync(id);
        return documento is null ? NotFound() : documento;
    }

    /// <summary>Elimina un documento. Restringido a Admin/Gerencia para mantener trazabilidad.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{Domain.Common.RolesApp.Admin},{Domain.Common.RolesApp.Gerencia}")]
    public async Task<IActionResult> Eliminar(int id) =>
        await service.EliminarAsync(id) ? NoContent() : NotFound();
}
