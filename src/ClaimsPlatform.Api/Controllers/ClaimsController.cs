using ClaimsPlatform.Api.Contracts;
using ClaimsPlatform.Api.Domain;
using ClaimsPlatform.Api.Infrastructure;
using ClaimsPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsPlatform.Api.Controllers;

[ApiController]
[Route("api/claims")]
public class ClaimsController(UserContext users, ClaimService claims) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ClaimDetailDto>> Submit(SubmitClaimRequest request, CancellationToken ct)
    {
        var user = await users.RequireAsync(UserRole.Claimant);
        var created = await claims.SubmitAsync(user, request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<ClaimSummaryDto>>> Mine(CancellationToken ct)
    {
        var user = await users.RequireAsync(UserRole.Claimant);
        return await claims.MineAsync(user.Id, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClaimDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var user = await users.RequireAsync();
        return await claims.GetAsync(user, id, ct);
    }

    [HttpPost("{id:guid}/info-requests/{requestId:guid}/respond")]
    public async Task<ActionResult<ClaimDetailDto>> Respond(Guid id, Guid requestId, RespondInfoRequest body, CancellationToken ct)
    {
        var user = await users.RequireAsync(UserRole.Claimant);
        return await claims.RespondAsync(user, id, requestId, body.Response, ct);
    }
}