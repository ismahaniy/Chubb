namespace Claims.Domain;

public sealed class Policy
{
    public Guid Id { get; private set; }
    public string PolicyNumber { get; private set; } = null!;
    public Guid HolderId { get; private set; }
    public Market Market { get; private set; }
    public ProductType ProductType { get; private set; }
    public Money CoverageLimit { get; private set; } = null!;
    public Money Deductible { get; private set; } = null!;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }

    private Policy()
    {
    }

    public Policy(
        string policyNumber,
        Guid holderId,
        Market market,
        ProductType productType,
        Money coverageLimit,
        Money deductible,
        DateOnly startDate,
        DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(policyNumber))
        {
            throw new DomainException("Policy number is required.");
        }

        if (!coverageLimit.IsGreaterThan(deductible))
        {
            throw new DomainException("Deductible must be less than the coverage limit.");
        }

        if (endDate <= startDate)
        {
            throw new DomainException("Policy end date must be after its start date.");
        }

        Id = Guid.CreateVersion7();
        PolicyNumber = policyNumber.Trim();
        HolderId = holderId;
        Market = market;
        ProductType = productType;
        CoverageLimit = coverageLimit;
        Deductible = deductible;
        StartDate = startDate;
        EndDate = endDate;
    }

    public string Currency => CoverageLimit.Currency;

    /// <summary>The most this policy can pay on one claim.</summary>
    public Money MaxPayable => CoverageLimit.Subtract(Deductible);

    public bool Covers(DateOnly date) => date >= StartDate && date <= EndDate;
}
