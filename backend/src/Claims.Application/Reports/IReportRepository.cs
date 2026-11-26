namespace Claims.Application.Reports;

public interface IReportRepository
{
    /// <summary>One line per market, product and currency that has open claims.</summary>
    Task<IReadOnlyList<ExposureLine>> GetExposureAsync(CancellationToken cancellationToken);
}
