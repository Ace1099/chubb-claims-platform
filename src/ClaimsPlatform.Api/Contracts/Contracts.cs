using ClaimsPlatform.Api.Domain;

namespace ClaimsPlatform.Api.Contracts;

// ---- Requests: the API contract. The backend defines it; Swagger publishes it; Angular codes against it. ----
public record SubmitClaimRequest(ClaimType Type, Market Market, DateOnly IncidentDate, string Description, decimal ClaimedAmount);
public record RespondInfoRequest(string Response);
public record RequestInfoRequest(string Question);
public record ApproveRequest(decimal ApprovedAmount, string Reason);
public record RejectRequest(string Reason);

// ---- Responses ----
public record UserDto(Guid Id, string Name, string Email, UserRole Role);
public record NotificationDto(Guid Id, Guid ClaimId, string Message, DateTime CreatedAt);

public record ClaimSummaryDto(Guid Id, string ClaimNumber, ClaimType Type, Market Market, ClaimStatus Status,
    decimal ClaimedAmount, DateTime CreatedAt, DateTime UpdatedAt, string? AssignedOfficerName, double AgeHours);
public record InfoRequestDto(Guid Id, string Question, DateTime RequestedAt, string? Response, DateTime? RespondedAt);
public record DecisionDto(bool IsApproved, decimal ApprovedAmount, string Reason, DateTime DecidedAt);
public record TimelineItemDto(ClaimStatus? From, ClaimStatus To, string? Note, DateTime OccurredAt);
public record ClaimDetailDto(Guid Id, string ClaimNumber, ClaimType Type, Market Market, ClaimStatus Status,
    decimal ClaimedAmount, DateTime CreatedAt, DateTime UpdatedAt, string? AssignedOfficerName, double AgeHours,
    string Description, DateOnly IncidentDate, string ClaimantName,
    List<InfoRequestDto> InfoRequests, DecisionDto? Decision, List<TimelineItemDto> Timeline);

public record OfficerWorkloadDto(int UnassignedCount, int UnderReview, int AwaitingClaimant,
    int AwaitingSettlement, List<ClaimSummaryDto> Items);

public record ExposureLineDto(string Key, int OpenClaims, decimal Exposure);
public record ManagerSummaryDto(int TotalClaims, int OpenClaims, decimal TotalExposure,
    Dictionary<string, int> ByStatus, List<ExposureLineDto> ByMarket, List<ExposureLineDto> ByType);
public record OfficerPerformanceDto(Guid OfficerId, string Name, int OpenClaims, int AwaitingClaimant,
    int Approved, int Rejected, int Settled, double? AvgHoursToDecision);

// Domain objects never leave the API; everything goes through these mappers.
public static class ClaimMapper
{
    // SQLite returns DateTimes with no 'Z'; mark them UTC so browsers show the right local time.
    public static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    public static ClaimSummaryDto ToSummary(Claim c) => new(
        c.Id, c.ClaimNumber, c.Type, c.Market, c.Status, c.ClaimedAmount,
        Utc(c.CreatedAt), Utc(c.UpdatedAt), c.AssignedOfficer?.Name,
        Math.Round((DateTime.UtcNow - Utc(c.CreatedAt)).TotalHours, 1));

    public static ClaimDetailDto ToDetail(Claim c)
    {
        var s = ToSummary(c);
        return new ClaimDetailDto(
            s.Id, s.ClaimNumber, s.Type, s.Market, s.Status, s.ClaimedAmount,
            s.CreatedAt, s.UpdatedAt, s.AssignedOfficerName, s.AgeHours,
            c.Description, c.IncidentDate, c.Claimant?.Name ?? "",
            c.InfoRequests.OrderBy(r => r.RequestedAt)
                .Select(r => new InfoRequestDto(r.Id, r.Question, Utc(r.RequestedAt), r.Response,
                    r.RespondedAt is null ? null : Utc(r.RespondedAt.Value))).ToList(),
            c.Decision is null ? null
                : new DecisionDto(c.Decision.IsApproved, c.Decision.ApprovedAmount, c.Decision.Reason, Utc(c.Decision.DecidedAt)),
            c.Events.OrderBy(e => e.OccurredAt)
                .Select(e => new TimelineItemDto(e.FromStatus, e.ToStatus, e.Note, Utc(e.OccurredAt))).ToList());
    }
}