using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockControl.Application.Importacion;
using StockControl.Domain.Common;

namespace StockControl.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RolesApp.Admin)]
public class ImportacionController(IImportadorExcelService service) : ControllerBase
{
    /// <summary>Importa un libro mensual de reportes (formato Excel de los hoteles).</summary>
    [HttpPost("excel")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<ResultadoImportacion>> Excel(IFormFile archivo)
    {
        if (archivo.Length == 0)
            return BadRequest(new { error = "El archivo está vacío." });

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (extension is not (".xlsx" or ".xlsm"))
            return BadRequest(new { error = "Solo se aceptan archivos .xlsx o .xlsm." });

        await using var stream = archivo.OpenReadStream();
        return await service.ImportarAsync(stream);
    }
}
