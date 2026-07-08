using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Dashboard;

namespace StockControl.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(IDashboardService service) : ControllerBase
{
    [HttpGet("resumen")]
    public async Task<ActionResult<ResumenMensualDto>> Resumen([FromQuery] int? anio, [FromQuery] int? mes)
    {
        var hoy = DateTime.UtcNow;
        return await service.ResumenMensualAsync(anio ?? hoy.Year, mes ?? hoy.Month);
    }

    [HttpGet("top-comprados")]
    public async Task<ActionResult<List<TopProductoDto>>> TopComprados(
        [FromQuery] int meses = 6, [FromQuery] int top = 10) =>
        await service.TopCompradosAsync(meses, top);

    [HttpGet("top-caros")]
    public async Task<ActionResult<List<TopProductoDto>>> TopCaros(
        [FromQuery] int meses = 6, [FromQuery] int top = 10) =>
        await service.TopCarosAsync(meses, top);

    [HttpGet("tendencia-precio/{productoId:int}")]
    public async Task<ActionResult<TendenciaPrecioDto>> TendenciaPrecio(
        int productoId, [FromQuery] int meses = 12)
    {
        var tendencia = await service.TendenciaPrecioAsync(productoId, meses);
        return tendencia is null ? NotFound() : tendencia;
    }

    [HttpGet("consumo-hoteles")]
    public async Task<ActionResult<List<ConsumoHotelSerieDto>>> ConsumoHoteles([FromQuery] int meses = 6) =>
        await service.ConsumoPorHotelAsync(meses);

    [HttpGet("alertas")]
    public async Task<ActionResult<List<AlertaPrecioDto>>> Alertas([FromQuery] decimal umbral = 15) =>
        await service.AlertasPrecioAsync(umbral);

    [HttpGet("gerencial")]
    public async Task<ActionResult<DashboardGerencialDto>> Gerencial([FromQuery] int? anio, [FromQuery] int? mes)
    {
        var hoy = DateTime.UtcNow;
        return await service.GerencialAsync(anio ?? hoy.Year, mes ?? hoy.Month);
    }
}
