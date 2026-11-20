using ClaimsPlatform.Api.Domain;
using Xunit;

namespace ClaimsPlatform.Tests;

public class ClaimDomainTests
{
    private static readonly Guid Officer = Guid.NewGuid();

    private static Claim NewClaim(decimal amount = 5000m) => Claim.Submit(
        Guid.NewGuid(), ClaimType.Motor, Market.Malaysia,
        DateOnly.FromDateTime(DateTime.UtcNow), "Rear-ended at traffic lights", amount);

    private static Claim UnderReview() { var c = NewClaim(); c.PickUp(Officer); return c; }

    [Theory]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.InfoRequested)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.Approved)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.Rejected)]
    [InlineData(ClaimStatus.InfoRequested, ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.Approved, ClaimStatus.Settled)]
    public void Valid_transitions_are_allowed(ClaimStatus from, ClaimStatus to)
        => Assert.True(ClaimStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.Settled)]
    [InlineData(ClaimStatus.InfoRequested, ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Rejected, ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Settled, ClaimStatus.UnderReview)]
    public void Invalid_transitions_are_blocked(ClaimStatus from, ClaimStatus to)
        => Assert.False(ClaimStateMachine.CanTransition(from, to));

    [Fact]
    public void Submit_creates_claim_with_audit_event()
    {
        var claim = NewClaim();
        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.StartsWith("CLM-", claim.ClaimNumber);
        Assert.Single(claim.Events);
    }

    [Theory]
    [InlineData("", 100.0)]
    [InlineData("Valid description", 0.0)]
    [InlineData("Valid description", -50.0)]
    public void Submit_rejects_invalid_input(string description, double amount)
        => Assert.Throws<InvalidInputException>(() => Claim.Submit(
            Guid.NewGuid(), ClaimType.Property, Market.Singapore,
            DateOnly.FromDateTime(DateTime.UtcNow), description, (decimal)amount));

    [Fact]
    public void Submit_rejects_future_incident_date()
        => Assert.Throws<InvalidInputException>(() => Claim.Submit(
            Guid.NewGuid(), ClaimType.Motor, Market.Malaysia,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10), "Future", 100m));

    [Fact]
    public void PickUp_assigns_officer_and_moves_to_UnderReview()
    {
        var claim = UnderReview();
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Equal(Officer, claim.AssignedOfficerId);
        Assert.Equal(2, claim.Events.Count);
    }

    [Fact]
    public void PickUp_twice_is_rejected()
        => Assert.Throws<DomainException>(() => UnderReview().PickUp(Guid.NewGuid()));

    [Fact]
    public void Only_the_assigned_officer_can_decide()
        => Assert.Throws<ForbiddenException>(() => UnderReview().Approve(Guid.NewGuid(), 100m, "x"));

    [Fact]
    public void RequestInfo_then_ProvideInfo_returns_claim_to_review()
    {
        var claim = UnderReview();
        var request = claim.RequestInfo(Officer, "Police report number?");
        Assert.Equal(ClaimStatus.InfoRequested, claim.Status);

        claim.ProvideInfo(claim.ClaimantId, request.Id, "KL-12345");

        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Equal("KL-12345", request.Response);
    }

    [Fact]
    public void ProvideInfo_by_someone_else_is_forbidden()
    {
        var claim = UnderReview();
        var request = claim.RequestInfo(Officer, "Q?");
        Assert.Throws<ForbiddenException>(() => claim.ProvideInfo(Guid.NewGuid(), request.Id, "A"));
    }

    [Fact]
    public void Approve_records_decision_and_caps_amount_at_claimed()
    {
        var claim = UnderReview();
        Assert.Throws<InvalidInputException>(() => claim.Approve(Officer, 99999m, "too much"));

        claim.Approve(Officer, 4500m, "Covered");

        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal(4500m, claim.Decision!.ApprovedAmount);
    }

    [Fact]
    public void Settled_claim_is_closed_and_cannot_change()
    {
        var claim = UnderReview();
        claim.Approve(Officer, 1000m, "ok");
        claim.Settle(Officer);

        Assert.False(ClaimStateMachine.IsOpen(claim.Status));
        Assert.Throws<DomainException>(() => claim.Reject(Officer, "too late"));
    }
}