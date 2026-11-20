using ClaimsPlatform.Api.Contracts;
using ClaimsPlatform.Api.Data;
using ClaimsPlatform.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClaimsPlatform.Api.Services;

public class ReportingService(ClaimsDbContext db)
{
    // Liability exposure = money still at risk for open claims:
    // the approved amount once approved (committed, unpaid); otherwise the claimed amount (potential).
    private static decimal ExposureOf(Claim c) =>
        c.Status == ClaimStatus.Approved && c.Decision is not null ? c.Decision.ApprovedAmount : c.ClaimedAmount;

    public async Task<ManagerSummaryDto> SummaryAsync(CancellationToken ct)
    {
        // Sprint simplification: aggregate in memory. At scale: SQL GROUP BY or a read model.
        var claims = await db.Claims.AsNoTracking().Include(c => c.Decision).ToListAsync(ct);
        var open = claims.Where(c => ClaimStateMachine.IsOpen(c.Status)).ToList();

        List<ExposureLineDto> Group<TKey>(Func<Claim, TKey> key) where TKey : notnull =>
            open.GroupBy(key)
                .Select(g => new ExposureLineDto(g.Key.ToString()!, g.Count(), g.Sum(c => ExposureOf(c))))
                .OrderByDescending(x => x.Exposure).ToList();

        return new ManagerSummaryDto(
            claims.Count, open.Count, open.Sum(c => ExposureOf(c)),
            Enum.GetValues<ClaimStatus>().ToDictionary(s => s.ToString(), s => claims.Count(c => c.Status == s)),
            Group(c => c.Market), Group(c => c.Type));
    }

    public async Task<List<OfficerPerformanceDto>> TeamPerformanceAsync(CancellationToken ct)
    {
        var officers = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Officer).ToListAsync(ct);
        var claims = await db.Claims.AsNoTracking().Include(c => c.Decision)
            .Where(c => c.AssignedOfficerId != null).ToListAsync(ct);

        return officers.Select(o =>
        {
            var mine = claims.Where(c => c.AssignedOfficerId == o.Id).ToList();
            var hours = mine.Where(c => c.Decision != null)
                .Select(c => (c.Decision!.DecidedAt - c.CreatedAt).TotalHours).ToList();
            return new OfficerPerformanceDto(o.Id, o.Name,
                mine.Count(c => ClaimStateMachine.IsOpen(c.Status)),
                mine.Count(c => c.Status == ClaimStatus.InfoRequested),
                mine.Count(c => c.Decision is { IsApproved: true }),
                mine.Count(c => c.Decision is { IsApproved: false }),
                mine.Count(c => c.Status == ClaimStatus.Settled),
                hours.Count == 0 ? null : Math.Round(hours.Average(), 1));
        }).OrderBy(p => p.Name).ToList();
    }
}