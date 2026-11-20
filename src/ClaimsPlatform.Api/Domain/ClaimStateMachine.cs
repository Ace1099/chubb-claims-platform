namespace ClaimsPlatform.Api.Domain;

public static class ClaimStateMachine
{
    private static readonly Dictionary<ClaimStatus, ClaimStatus[]> Allowed = new()
    {
        [ClaimStatus.Submitted]     = [ClaimStatus.UnderReview],
        [ClaimStatus.UnderReview]   = [ClaimStatus.InfoRequested, ClaimStatus.Approved, ClaimStatus.Rejected],
        [ClaimStatus.InfoRequested] = [ClaimStatus.UnderReview],
        [ClaimStatus.Approved]      = [ClaimStatus.Settled],
        [ClaimStatus.Rejected]      = [],
        [ClaimStatus.Settled]       = [],
    };

    public static bool CanTransition(ClaimStatus from, ClaimStatus to) => Allowed[from].Contains(to);

    public static void EnsureCanTransition(ClaimStatus from, ClaimStatus to)
    {
        if (!CanTransition(from, to))
            throw new DomainException($"Cannot move claim from {from} to {to}.");
    }

    // "Open" = still carries liability for the insurer.
    public static bool IsOpen(ClaimStatus status) =>
        status is not (ClaimStatus.Rejected or ClaimStatus.Settled);
}