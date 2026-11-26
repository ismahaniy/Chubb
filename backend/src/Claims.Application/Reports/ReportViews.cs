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
