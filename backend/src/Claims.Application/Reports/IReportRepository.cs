namespace Claims.Application.Reports;

public interface IReportRepository
{
    /// <summary>One line per market, product and currency that has open claims.</summary>
    Task<IReadOnlyList<ExposureLine>> GetExposureAsync(CancellationToken cancellationToken);

    /// <summary>One row per officer. <paramref name="now"/> decides SLA breaches and the 7-day settlement window.</summary>
    Task<IReadOnlyList<OfficerWorkload>> GetWorkloadAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
