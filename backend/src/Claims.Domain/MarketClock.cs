namespace Claims.Domain;

/// <summary>
/// The calendar date in a market. Policy periods and incident dates are local dates, so "today" must be the
/// market's date, not UTC's: 07:00 in Singapore is still the previous day in UTC.
/// None of the six markets observes daylight saving, so a fixed offset is exact.
/// </summary>
public static class MarketClock
{
    public static TimeSpan UtcOffset(Market market) => market switch
    {
        Market.Singapore or Market.HongKong or Market.Malaysia or Market.Philippines => TimeSpan.FromHours(8),
        Market.Thailand or Market.Indonesia => TimeSpan.FromHours(7),
        _ => throw new ArgumentOutOfRangeException(nameof(market), market, null),
    };

    public static DateOnly Today(Market market, DateTimeOffset now) =>
        DateOnly.FromDateTime(now.ToOffset(UtcOffset(market)).DateTime);
}
