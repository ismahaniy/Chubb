using Claims.Application.Claims;
using Claims.Domain;

namespace Claims.Application.Reports;

/// <summary>
/// Outstanding liability for one market, product and currency. Each claim counts at its reserve, or at its claimed
/// amount while no reserve is recorded. Amounts are never added across currencies, so a market and product with claims
/// in two currencies has two lines.
/// </summary>
/// <param name="OpenClaims">Claims not yet Rejected or Settled (Approved claims are still unpaid, so they count).</param>
public sealed record ExposureLine(Market Market, ProductType ProductType, int OpenClaims, MoneyDto Exposure);

/// <summary>Exposure lines ordered by market, product and currency.</summary>
public sealed record ExposureReport(DateTimeOffset AsOf, IReadOnlyList<ExposureLine> Lines);

/// <param name="OpenClaims">Assigned claims not yet Rejected or Settled.</param>
/// <param name="SlaBreaches">Assigned claims still awaiting a decision and past their SLA due date.</param>
/// <param name="SettledLast7Days">Claims this officer settled in the 7 days up to the report time.</param>
public sealed record OfficerWorkload(Guid OfficerId, string Name, int OpenClaims, int SlaBreaches, int SettledLast7Days);

/// <summary>Every officer, including those with no work, ordered by name.</summary>
public sealed record WorkloadReport(DateTimeOffset AsOf, IReadOnlyList<OfficerWorkload> Officers);
