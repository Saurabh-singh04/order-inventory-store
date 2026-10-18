using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Store.Api.Infrastructure;
using Store.Application.Reports;

namespace Store.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = SecuritySetup.AdminRole)]
[Produces("application/json")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    /// <summary>Top customers by spend (Confirmed + Shipped) in the last N days.</summary>
    [HttpGet("top-customers")]
    [ProducesResponseType<IReadOnlyList<TopCustomerDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TopCustomerDto>>> TopCustomers(
        [FromQuery] int days = 30, [FromQuery] int top = 5, CancellationToken cancellationToken = default) =>
        Ok(await _reports.GetTopCustomersAsync(days, top, cancellationToken));
}
