namespace Claims.Domain;

public enum Role
{
    Claimant,
    Officer,
    Manager,
}

public enum Market
{
    Singapore,
    HongKong,
    Malaysia,
    Thailand,
    Indonesia,
    Philippines,
}

public enum ProductType
{
    Motor,
    Property,
}

public enum ClaimStatus
{
    Submitted,
    UnderReview,
    AwaitingInformation,
    Approved,
    Rejected,
    Settled,
}

public enum DecisionOutcome
{
    Approved,
    Rejected,
}
