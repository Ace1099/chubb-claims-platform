using ClaimsPlatform.Api.Contracts;
using ClaimsPlatform.Api.Data;
using ClaimsPlatform.Api.Domain;
using ClaimsPlatform.Api.Events;
using Microsoft.EntityFrameworkCore;

namespace ClaimsPlatform.Api.Services;

public class ClaimService(ClaimsDbContext db, IEventPublisher events, ILogger<ClaimService> log)
{
    private IQueryable<Claim> Full() => db.Claims
        .Include(c => c.Claimant).Include(c => c.AssignedOfficer)
        .Include(c => c.Decision).Include(c => c.InfoRequests).Include(c => c.Events)
        .AsSplitQuery();

    private async Task<Claim> LoadAsync(Guid id, CancellationToken ct) =>
        await Full().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Claim not found.");

    private async Task<ClaimDetailDto> ReadDetailAsync(Guid id, CancellationToken ct) =>
        ClaimMapper.ToDetail(await Full().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Claim not found."));

    // The DB is the source of truth: a failed publish is logged, it does not fail the user's request.
    // (Production fix for lost events: transactional outbox.)
    private async Task PublishAsync(ClaimDomainEvent evt, CancellationToken ct)
    {
        try { await events.PublishAsync(evt, ct); }
        catch (Exception ex) { log.LogError(ex, "Failed to publish {Type} for claim {ClaimId}", evt.Type, evt.ClaimId); }
    }

    // Load -> apply domain rule -> save -> publish event -> return fresh view.
    private async Task<ClaimDetailDto> MutateAsync(Guid id, Action<Claim> action, string eventType, string detail, CancellationToken ct)
    {
        var claim = await LoadAsync(id, ct);
        action(claim);
        await db.SaveChangesAsync(ct);
        await PublishAsync(ClaimDomainEvent.From(eventType, claim, detail), ct);
        return await ReadDetailAsync(id, ct);
    }

    // ---------- Claimant ----------
    public async Task<ClaimDetailDto> SubmitAsync(User user, SubmitClaimRequest req, CancellationToken ct)
    {
        var claim = Claim.Submit(user.Id, req.Type, req.Market, req.IncidentDate, req.Description, req.ClaimedAmount);
        db.Claims.Add(claim);
        await db.SaveChangesAsync(ct);
        await PublishAsync(ClaimDomainEvent.From(EventTypes.ClaimSubmitted, claim, "Claim submitted"), ct);
        return await ReadDetailAsync(claim.Id, ct);
    }

    public async Task<List<ClaimSummaryDto>> MineAsync(Guid claimantId, CancellationToken ct) =>
        (await db.Claims.AsNoTracking().Include(c => c.AssignedOfficer)
            .Where(c => c.ClaimantId == claimantId)
            .OrderByDescending(c => c.CreatedAt).ToListAsync(ct))
        .Select(ClaimMapper.ToSummary).ToList();

    public async Task<ClaimDetailDto> GetAsync(User user, Guid id, CancellationToken ct)
    {
        var claim = await Full().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Claim not found.");
        if (user.Role == UserRole.Claimant && claim.ClaimantId != user.Id)
            throw new ForbiddenException("This is not your claim.");
        return ClaimMapper.ToDetail(claim);
    }

    public Task<ClaimDetailDto> RespondAsync(User user, Guid claimId, Guid requestId, string response, CancellationToken ct) =>
        MutateAsync(claimId, c => c.ProvideInfo(user.Id, requestId, response),
            EventTypes.InfoProvided, "Claimant provided information", ct);

    // ---------- Officer ----------
    public async Task<List<ClaimSummaryDto>> QueueAsync(CancellationToken ct) =>
        (await db.Claims.AsNoTracking().Include(c => c.AssignedOfficer)
            .Where(c => c.Status == ClaimStatus.Submitted && c.AssignedOfficerId == null)
            .OrderBy(c => c.CreatedAt).ToListAsync(ct))
        .Select(ClaimMapper.ToSummary).ToList();

    public async Task<OfficerWorkloadDto> WorkloadAsync(Guid officerId, CancellationToken ct)
    {
        var mine = await db.Claims.AsNoTracking().Include(c => c.AssignedOfficer)
            .Where(c => c.AssignedOfficerId == officerId &&
                (c.Status == ClaimStatus.UnderReview || c.Status == ClaimStatus.InfoRequested || c.Status == ClaimStatus.Approved))
            .OrderBy(c => c.CreatedAt).ToListAsync(ct);
        var unassigned = await db.Claims.CountAsync(
            c => c.Status == ClaimStatus.Submitted && c.AssignedOfficerId == null, ct);

        return new OfficerWorkloadDto(unassigned,
            mine.Count(c => c.Status == ClaimStatus.UnderReview),
            mine.Count(c => c.Status == ClaimStatus.InfoRequested),
            mine.Count(c => c.Status == ClaimStatus.Approved),
            mine.Select(ClaimMapper.ToSummary).ToList());
    }

    public Task<ClaimDetailDto> PickUpAsync(User o, Guid id, CancellationToken ct) =>
        MutateAsync(id, c => c.PickUp(o.Id), EventTypes.ClaimPickedUp, "Claim picked up for review", ct);

    public Task<ClaimDetailDto> RequestInfoAsync(User o, Guid id, string question, CancellationToken ct) =>
        MutateAsync(id, c => c.RequestInfo(o.Id, question), EventTypes.InfoRequested, question, ct);

    public Task<ClaimDetailDto> ApproveAsync(User o, Guid id, decimal amount, string reason, CancellationToken ct) =>
        MutateAsync(id, c => c.Approve(o.Id, amount, reason), EventTypes.ClaimApproved,
            $"Approved amount: {amount:N2}. {reason}", ct);

    public Task<ClaimDetailDto> RejectAsync(User o, Guid id, string reason, CancellationToken ct) =>
        MutateAsync(id, c => c.Reject(o.Id, reason), EventTypes.ClaimRejected, reason, ct);

    public Task<ClaimDetailDto> SettleAsync(User o, Guid id, CancellationToken ct) =>
        MutateAsync(id, c => c.Settle(o.Id), EventTypes.ClaimSettled, "Payment settled", ct);
}