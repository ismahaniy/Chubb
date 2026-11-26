namespace Claims.Domain;

/// <summary>An amount in one ISO 4217 currency. Operations across currencies throw.</summary>
public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (currency is not { Length: 3 } || !currency.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException($"'{currency}' is not an ISO 4217 currency code.");
        }

        if (amount < 0)
        {
            throw new DomainException("Amount cannot be negative.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("Amount cannot have more than 2 decimal places.");
        }

        Amount = amount;
        Currency = currency;
    }

    public bool IsZero => Amount == 0;

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public bool IsGreaterThan(Money other)
    {
        EnsureSameCurrency(other);
        return Amount > other.Amount;
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";

    private void EnsureSameCurrency(Money other)
    {
        if (other.Currency != Currency)
        {
            throw new DomainException($"Cannot combine {Currency} with {other.Currency}.");
        }
    }
}
