namespace ClaimsPlatform.Api.Domain;

public class Claim
{
    private Claim() { } // for EF Core only; everyone else must use Claim.Submit(...)

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ClaimNumber { get; private set; } = "";
    public ClaimType Type { get; private set; }
    public Market Market { get; private set; }
    public ClaimStatus Status { get; private set; } = ClaimStatus.Submitted;
    public DateOnly IncidentDate { get; private set; }
    public string Description { get; private set; } = "";
    public decimal ClaimedAmount { get; private set; }
    public int Version { get; private set; } // optimistic-concurrency token

    public Guid ClaimantId { get; private set; }
    public User? Claimant { get; private set; }
    public Guid? AssignedOfficerId { get; private set; }
    public User? AssignedOfficer { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public Decision? Decision { get; private set; }
    public List<InfoRequest> InfoRequests { get; private set; } = [];
    public List<ClaimEvent> Events { get; private set; } = [];

    public static Claim Submit(Guid claimantId, ClaimType type, Market market,
        DateOnly incidentDate, string description, decimal claimedAmount)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidInputException("Description is required.");
        if (claimedAmount <= 0)
            throw new InvalidInputException("Claimed amount must be greater than zero.");
        // +1 day tolerance because APAC time zones can be ahead of UTC.
        if (incidentDate > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            throw new InvalidInputException("Incident date cannot be in the future.");

        var claim = new Claim
        {
            ClaimantId = claimantId,
            Type = type,
            Market = market,
            IncidentDate = incidentDate,
            Description = description.Trim(),
            ClaimedAmount = claimedAmount,
            ClaimNumber = $"CLM-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"
        };
        claim.Events.Add(new ClaimEvent
        {
            ClaimId = claim.Id, FromStatus = null, ToStatus = ClaimStatus.Submitted,
            ActorId = claimantId, Note = "Claim submitted"
        });
        return claim;
    }

    // ---- Workflow: every business rule lives here, not in controllers ----

    public void PickUp(Guid officerId)
    {
        if (AssignedOfficerId is not null)
            throw new DomainException("This claim has already been picked up.");
        ChangeStatus(ClaimStatus.UnderReview, officerId, "Picked up for review");
        AssignedOfficerId = officerId;
    }

    public InfoRequest RequestInfo(Guid officerId, string question)
    {
        EnsureAssignedTo(officerId);
        if (string.IsNullOrWhiteSpace(question))
            throw new InvalidInputException("A question is required.");
        ChangeStatus(ClaimStatus.InfoRequested, officerId, "Additional information requested");
        var request = new InfoRequest { ClaimId = Id, RequestedByOfficerId = officerId, Question = question.Trim() };
        InfoRequests.Add(request);
        return request;
    }

    public void ProvideInfo(Guid claimantId, Guid requestId, string response)
    {
        if (claimantId != ClaimantId)
            throw new ForbiddenException("You can only respond on your own claims.");
        var request = InfoRequests.FirstOrDefault(r => r.Id == requestId)
            ?? throw new NotFoundException("Information request not found.");
        if (request.Response is not null)
            throw new DomainException("This request has already been answered.");
        if (string.IsNullOrWhiteSpace(response))
            throw new InvalidInputException("A response is required.");
        ChangeStatus(ClaimStatus.UnderReview, claimantId, "Claimant provided information");
        request.Response = response.Trim();
        request.RespondedAt = DateTime.UtcNow;
    }

    public void Approve(Guid officerId, decimal approvedAmount, string reason)
    {
        EnsureAssignedTo(officerId);
        if (approvedAmount <= 0 || approvedAmount > ClaimedAmount)
            throw new InvalidInputException("Approved amount must be above 0 and not exceed the claimed amount.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidInputException("A reason is required.");
        ChangeStatus(ClaimStatus.Approved, officerId, reason.Trim());
        Decision = new Decision
        {
            ClaimId = Id, DecidedByOfficerId = officerId, IsApproved = true,
            ApprovedAmount = approvedAmount, Reason = reason.Trim()
        };
    }

    public void Reject(Guid officerId, string reason)
    {
        EnsureAssignedTo(officerId);
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidInputException("A reason is required.");
        ChangeStatus(ClaimStatus.Rejected, officerId, reason.Trim());
        Decision = new Decision
        {
            ClaimId = Id, DecidedByOfficerId = officerId, IsApproved = false,
            ApprovedAmount = 0, Reason = reason.Trim()
        };
    }

    public void Settle(Guid officerId)
    {
        EnsureAssignedTo(officerId);
        ChangeStatus(ClaimStatus.Settled, officerId, "Payment settled");
    }

    public void ChangeStatus(ClaimStatus next, Guid actorId, string? note = null)
    {
        ClaimStateMachine.EnsureCanTransition(Status, next);
        Events.Add(new ClaimEvent
        {
            ClaimId = Id, FromStatus = Status, ToStatus = next, ActorId = actorId, Note = note
        });
        Status = next;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    private void EnsureAssignedTo(Guid officerId)
    {
        if (AssignedOfficerId != officerId)
            throw new ForbiddenException("Only the officer assigned to this claim can do that.");
    }
}