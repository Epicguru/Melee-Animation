using Microsoft.AspNetCore.Mvc;
using ModRequestAPI.Backend.Facade;
using ModRequestAPI.Models;

namespace ModRequestAPI.Backend.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ModReportingController : ControllerBase
{
    private readonly ModReportingFacade facade;

    public ModReportingController(ModReportingFacade facade)
    {
        this.facade = facade;
    }

    [HttpPost("report-missing-mods")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(string))]
    public async Task<IActionResult> ReportMissingMod([FromBody] IEnumerable<MissingModRequest?> mods)
    {
        string? errorMsg = await facade.ReportMissingModAsync(mods);
        if (errorMsg != null)
            return BadRequest($"Error: {errorMsg}");

        return Ok();
    }

    [HttpHead("health-check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult HealthCheck()
    {
        return Ok();
    }
}