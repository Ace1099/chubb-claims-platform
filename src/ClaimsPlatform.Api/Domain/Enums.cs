namespace ClaimsPlatform.Api.Domain;

public enum UserRole { Claimant, Officer, Manager }
public enum ClaimType { Motor, Property }
// The brief says "six markets" but does not name them: this list is an assumption.
public enum Market { Singapore, Malaysia, Indonesia, Thailand, HongKong, Philippines }
public enum ClaimStatus { Submitted, UnderReview, InfoRequested, Approved, Rejected, Settled }