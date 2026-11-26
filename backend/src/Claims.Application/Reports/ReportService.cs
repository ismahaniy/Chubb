namespace Claims.Application.Reports;

/// <summary>Manager reports. Read-only: nothing here changes a claim.</summary>
public sealed class ReportService(IReportRepository reports, TimeProvider clock)
{
    public async Task<ExposureReport> GetExposureAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return new ExposureReport(now, await reports.GetExposureAsync(cancellationToken));
    }
}
