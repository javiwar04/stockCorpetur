using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Reportes;
using StockControl.Domain.Common;

namespace StockControl.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportesController(IReporteService service) : ControllerBase
{
    [HttpGet("compras.xlsx")]
    public async Task<IActionResult> Excel(
        [FromQuery] int? hotelId,
        [FromQuery] int? proveedorId,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta)
    {
        var bytes = await service.GenerarExcelAsync(new FiltroReporte(hotelId, proveedorId, desde, hasta));
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"reporte-compras-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpGet("compras.pdf")]
    public async Task<IActionResult> Pdf(
        [FromQuery] int? hotelId,
        [FromQuery] int? proveedorId,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta)
    {
        var bytes = await service.GenerarPdfAsync(new FiltroReporte(hotelId, proveedorId, desde, hasta));
        return File(bytes, "application/pdf", $"reporte-compras-{DateTime.Now:yyyyMMdd}.pdf");
    }

    [HttpGet("kardex.xlsx")]
    public async Task<IActionResult> KardexExcel(
        [FromQuery] int hotelId,
        [FromQuery] int productoId,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta)
    {
        var bytes = await service.GenerarKardexExcelAsync(new FiltroReporteKardex(hotelId, productoId, desde, hasta));
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"kardex-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpGet("cuentas-por-pagar.xlsx")]
    public async Task<IActionResult> CuentasPorPagarExcel(
        [FromQuery] int? hotelId,
        [FromQuery] int? proveedorId,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] bool soloPendientes = true)
    {
        var bytes = await service.GenerarCuentasPorPagarExcelAsync(
            new FiltroReporteCuentasPorPagar(hotelId, proveedorId, desde, hasta, soloPendientes));
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"cuentas-por-pagar-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpGet("conteos-inventario.xlsx")]
    public async Task<IActionResult> ConteosExcel(
        [FromQuery] int? hotelId,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta)
    {
        var bytes = await service.GenerarConteosExcelAsync(new FiltroReporteConteos(hotelId, desde, hasta));
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"conteos-inventario-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpGet("conteos-inventario.pdf")]
    public async Task<IActionResult> ConteosPdf(
        [FromQuery] int? hotelId,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta)
    {
        var bytes = await service.GenerarConteosPdfAsync(new FiltroReporteConteos(hotelId, desde, hasta));
        return File(bytes, "application/pdf", $"conteos-inventario-{DateTime.Now:yyyyMMdd}.pdf");
    }

    [HttpGet("cierres-mensuales.xlsx")]
    [Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
    public async Task<IActionResult> CierresMensualesExcel(
        [FromQuery] int? hotelId,
        [FromQuery] int? anio,
        [FromQuery] int? mes)
    {
        var bytes = await service.GenerarCierresMensualesExcelAsync(new FiltroReporteCierresMensuales(hotelId, anio, mes));
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"cierres-mensuales-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpGet("cierres-mensuales.pdf")]
    [Authorize(Roles = $"{RolesApp.Admin},{RolesApp.Gerencia}")]
    public async Task<IActionResult> CierresMensualesPdf(
        [FromQuery] int? hotelId,
        [FromQuery] int? anio,
        [FromQuery] int? mes)
    {
        var bytes = await service.GenerarCierresMensualesPdfAsync(new FiltroReporteCierresMensuales(hotelId, anio, mes));
        return File(bytes, "application/pdf", $"cierres-mensuales-{DateTime.Now:yyyyMMdd}.pdf");
    }
}
