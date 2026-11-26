using Claims.Domain;

namespace Claims.UnitTests.Domain;

public sealed class MoneyTests
{
    [Theory]
    [InlineData("sgd")]
    [InlineData("SG")]
    [InlineData("SGDX")]
    [InlineData("S1D")]
    [InlineData("")]
    public void Invalid_currency_throws(string currency) =>
        Should.Throw<DomainException>(() => new Money(1m, currency));

    [Fact]
    public void Negative_amount_throws() =>
        Should.Throw<DomainException>(() => new Money(-0.01m, "SGD"));

    [Fact]
    public void More_than_two_decimals_throws() =>
        Should.Throw<DomainException>(() => new Money(1.001m, "SGD"));

    [Fact]
    public void Equality_is_by_value_and_ignores_trailing_zeros()
    {
        new Money(10.0m, "SGD").ShouldBe(new Money(10.00m, "SGD"));
        new Money(10m, "SGD").ShouldNotBe(new Money(10m, "HKD"));
    }

    [Fact]
    public void Subtract_and_compare_same_currency()
    {
        new Money(10m, "SGD").Subtract(new Money(2.5m, "SGD")).ShouldBe(new Money(7.5m, "SGD"));
        new Money(10m, "SGD").IsGreaterThan(new Money(9.99m, "SGD")).ShouldBeTrue();
        new Money(10m, "SGD").IsGreaterThan(new Money(10m, "SGD")).ShouldBeFalse();
    }

    [Fact]
    public void Cross_currency_operations_throw()
    {
        var sgd = new Money(10m, "SGD");
        var hkd = new Money(1m, "HKD");

        Should.Throw<DomainException>(() => sgd.Subtract(hkd));
        Should.Throw<DomainException>(() => sgd.IsGreaterThan(hkd));
    }

    [Fact]
    public void Subtract_below_zero_throws() =>
        Should.Throw<DomainException>(() => new Money(1m, "SGD").Subtract(new Money(2m, "SGD")));
}
