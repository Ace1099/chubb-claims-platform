using ClaimsPlatform.Api.Contracts;
using ClaimsPlatform.Api.Domain;
using ClaimsPlatform.Api.Infrastructure;
using ClaimsPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsPlatform.Api.Controllers;

[ApiController]
[Route("api/officer")]
public class OfficerController(UserContext users, ClaimService claims) : ControllerBase
{
    [HttpGet("workload")]
    public async Task<ActionResult<OfficerWorkloadDto>> Workload(CancellationToken ct)
    {
        var user = await users.RequireAsync(UserRole.Officer);
        return await claims.WorkloadAsync(user.Id, ct);
    }

    [HttpGet("queue")]
    public async Task<ActionResult<List<ClaimSummaryDto>>> Queue(CancellationToken ct)
    {
        await users.RequireAsync(UserRole.Officer);
        return await claims.QueueAsync(ct);
    }

    [HttpPost("claims/{id:guid}/pickup")]
    public async Task<ActionResult<ClaimDetailDto>> PickUp(Guid id, CancellationToken ct)
        => await claims.PickUpAsync(await users.RequireAsync(UserRole.Officer), id, ct);

    [HttpPost("claims/{id:guid}/request-info")]
    public async Task<ActionResult<ClaimDetailDto>> RequestInfo(Guid id, RequestInfoRequest body, CancellationToken ct)
        => await claims.RequestInfoAsync(await users.RequireAsync(UserRole.Officer), id, body.Question, ct);

    [HttpPost("claims/{id:guid}/approve")]
    public async Task<ActionResult<ClaimDetailDto>> Approve(Guid id, ApproveRequest body, CancellationToken ct)
        => await claims.ApproveAsync(await users.RequireAsync(UserRole.Officer), id, body.ApprovedAmount, body.Reason, ct);

    [HttpPost("claims/{id:guid}/reject")]
    public async Task<ActionResult<ClaimDetailDto>> Reject(Guid id, RejectRequest body, CancellationToken ct)
        => await claims.RejectAsync(await users.RequireAsync(UserRole.Officer), id, body.Reason, ct);

    [HttpPost("claims/{id:guid}/settle")]
    public async Task<ActionResult<ClaimDetailDto>> Settle(Guid id, CancellationToken ct)
        => await claims.SettleAsync(await users.RequireAsync(UserRole.Officer), id, ct);
}