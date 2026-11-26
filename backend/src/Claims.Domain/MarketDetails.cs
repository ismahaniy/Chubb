namespace Claims.Domain;

/// <summary>Reference data for a market: display name and the currency its policies are written in.</summary>
public sealed class MarketDetails
{
    public Market Code { get; private set; }
    public string Name { get; private set; } = null!;
    public string Currency { get; private set; } = null!;

    private MarketDetails()
    {
    }

    public MarketDetails(Market code, string name, string currency)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Market name is required.");
        }

        Code = code;
        Name = name.Trim();

        // Reuse Money's ISO 4217 check rather than duplicating it.
        Currency = new Money(0, currency).Currency;
    }
}
