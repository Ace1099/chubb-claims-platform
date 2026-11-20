namespace ClaimsPlatform.Api.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public UserRole Role { get; set; }
}

public class InfoRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClaimId { get; set; }
    public Guid RequestedByOfficerId { get; set; }
    public string Question { get; set; } = "";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? Response { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public class Decision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClaimId { get; set; }
    public Guid DecidedByOfficerId { get; set; }
    public bool IsApproved { get; set; }
    public decimal ApprovedAmount { get; set; } // 0 when rejected
    public string Reason { get; set; } = "";
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
}

// Audit trail: one row per status change.
public class ClaimEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClaimId { get; set; }
    public ClaimStatus? FromStatus { get; set; } // null for the very first event
    public ClaimStatus ToStatus { get; set; }
    public Guid ActorId { get; set; }
    public string? Note { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

// Created asynchronously by the event consumer (Step 4).
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ClaimId { get; set; }
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid SourceEventId { get; set; } // makes the consumer idempotent
}