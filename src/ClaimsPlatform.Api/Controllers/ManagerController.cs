using ClaimsPlatform.Api.Contracts;
using ClaimsPlatform.Api.Domain;
using ClaimsPlatform.Api.Infrastructure;
using ClaimsPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsPlatform.Api.Controllers;

[ApiController]
[Route("api/manager")]
public class ManagerController(UserContext users, ReportingService reports) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ManagerSummaryDto>> Summary(CancellationToken ct)
    {
        await users.RequireAsync(UserRole.Manager);
        return await reports.SummaryAsync(ct);
    }

    [HttpGet("team-performance")]
    public async Task<ActionResult<List<OfficerPerformanceDto>>> TeamPerformance(CancellationToken ct)
    {
        await users.RequireAsync(UserRole.Manager);
        return await reports.TeamPerformanceAsync(ct);
    }
}