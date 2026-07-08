using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Inventario;
using StockControl.Domain.Common;

namespace StockControl.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventarioController(IInventarioService service) : ControllerBase
{
    [HttpGet("existencias/{hotelId:int}")]
    public async Task<ActionResult<List<ExistenciaDto>>> Existencias(int hotelId) =>
        await service.ExistenciasAsync(hotelId);

    [HttpGet("stock-minimo/{hotelId:int}")]
    public async Task<ActionResult<List<StockMinimoDto>>> StockMinimo(int hotelId) =>
        await service.ListarStockMinimoAsync(hotelId);

    [HttpGet("alertas-stock")]
    public async Task<ActionResult<List<AlertaStockDto>>> AlertasStock() =>
        await service.AlertasStockAsync();

    [HttpGet("sugerencias-compra/{hotelId:int}")]
    public async Task<ActionResult<List<SugerenciaCompraDto>>> SugerenciasCompra(int hotelId) =>
        await service.SugerenciasCompraAsync(hotelId);

    [HttpGet("kardex")]
    public async Task<ActionResult<KardexDto>> Kardex(
        [FromQuery] int hotelId,
        [FromQuery] int productoId,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta) =>
        await service.KardexAsync(new FiltroKardex(hotelId, productoId, desde, hasta));

    [HttpPut("stock-minimo")]
    [Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
    public async Task<ActionResult<StockMinimoDto>> GuardarStockMinimo(GuardarStockMinimoRequest req) =>
        await service.GuardarStockMinimoAsync(req);

    [HttpDelete("stock-minimo/{hotelId:int}/{productoId:int}")]
    [Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
    public async Task<IActionResult> EliminarStockMinimo(int hotelId, int productoId) =>
        await service.EliminarStockMinimoAsync(hotelId, productoId) ? NoContent() : NotFound();

    [HttpGet("movimientos")]
    public async Task<ActionResult<List<MovimientoDto>>> Movimientos(
        [FromQuery] int? hotelId, [FromQuery] int? productoId,
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta) =>
        await service.ListarMovimientosAsync(new FiltroMovimientos(hotelId, productoId, desde, hasta));

    [HttpPost("movimientos")]
    public async Task<ActionResult<MovimientoDto>> Registrar(CrearMovimientoRequest req) =>
        await service.RegistrarMovimientoAsync(req);

    /// <summary>Solo Admin/Gerencia: revierte un movimiento mal registrado.</summary>
    [HttpDelete("movimientos/{id:int}")]
    [Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
    public async Task<IActionResult> Eliminar(int id) =>
        await service.EliminarMovimientoAsync(id) ? NoContent() : NotFound();
}
