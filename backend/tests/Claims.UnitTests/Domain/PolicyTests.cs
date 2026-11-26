using Claims.Domain;

namespace Claims.UnitTests.Domain;

public sealed class PolicyTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2026, 12, 31);

    private static Policy Create(Money limit, Money deductible, DateOnly? start = null, DateOnly? end = null) =>
        new("POL-1", Guid.CreateVersion7(), Market.HongKong, ProductType.Property, limit, deductible, start ?? Start, end ?? End);

    [Fact]
    public void MaxPayable_is_limit_minus_deductible() =>
        Create(new Money(50_000m, "HKD"), new Money(1_000m, "HKD")).MaxPayable.ShouldBe(new Money(49_000m, "HKD"));

    [Fact]
    public void Deductible_not_below_limit_throws() =>
        Should.Throw<DomainException>(() => Create(new Money(1_000m, "HKD"), new Money(1_000m, "HKD")));

    [Fact]
    public void Mixed_currencies_throw() =>
        Should.Throw<DomainException>(() => Create(new Money(1_000m, "HKD"), new Money(100m, "SGD")));

    [Fact]
    public void End_not_after_start_throws() =>
        Should.Throw<DomainException>(() => Create(new Money(1_000m, "HKD"), new Money(100m, "HKD"), Start, Start));

    [Fact]
    public void Blank_policy_number_throws() =>
        Should.Throw<DomainException>(() =>
            new Policy(" ", Guid.CreateVersion7(), Market.HongKong, ProductType.Property,
                new Money(1_000m, "HKD"), new Money(100m, "HKD"), Start, End));

    [Theory]
    [InlineData(2026, 1, 1, true)]
    [InlineData(2026, 12, 31, true)]
    [InlineData(2025, 12, 31, false)]
    [InlineData(2027, 1, 1, false)]
    public void Covers_includes_both_ends(int y, int m, int d, bool expected) =>
        Create(new Money(1_000m, "HKD"), new Money(100m, "HKD")).Covers(new DateOnly(y, m, d)).ShouldBe(expected);
}
