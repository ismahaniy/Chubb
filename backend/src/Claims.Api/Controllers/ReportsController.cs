using Claims.Api.Auth;
using Claims.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

/// <summary>Manager reports. Read-only.</summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policies.Manager)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    /// <summary>
    /// Outstanding liability on open claims (not yet Rejected or Settled), by market, product and currency. Each claim
    /// counts at its reserve, or its claimed amount while no reserve is recorded. Never summed across currencies.
    /// </summary>
    [HttpGet("exposure")]
    [ProducesResponseType<ExposureReport>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExposureReport>> Exposure(CancellationToken cancellationToken) =>
        await reports.GetExposureAsync(cancellationToken);

    /// <summary>Per officer: open claims, SLA breaches, and claims settled in the last 7 days.</summary>
    [HttpGet("workload")]
    [ProducesResponseType<WorkloadReport>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkloadReport>> Workload(CancellationToken cancellationToken) =>
        await reports.GetWorkloadAsync(cancellationToken);
}
