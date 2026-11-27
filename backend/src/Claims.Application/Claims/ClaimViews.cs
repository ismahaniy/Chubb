using Claims.Domain;

namespace Claims.Application.Claims;

public sealed record MoneyDto(decimal Amount, string Currency);

public sealed record ClaimSummary(
    Guid Id,
    string ClaimNumber,
    string PolicyNumber,
    ProductType ProductType,
    Market Market,
    ClaimStatus Status,
    MoneyDto ClaimedAmount,
    DateTimeOffset SubmittedAt);

/// <summary>
/// The claimant-facing view. Internal assessment data (reserve, notes, assigned officer) is deliberately left out;
/// staff get their own view in the officer slice.
/// </summary>
public sealed record ClaimDetail(
    Guid Id,
    string ClaimNumber,
    string PolicyNumber,
    Guid ClaimantId,
    ProductType ProductType,
    Market Market,
    ClaimStatus Status,
    DateOnly IncidentDate,
    string Description,
    MoneyDto ClaimedAmount,
    MoneyDto MaxPayable,
    DateTimeOffset SubmittedAt,
    DecisionDto? Decision,
    IReadOnlyList<StatusChangeDto> Timeline,
    IReadOnlyList<InformationRequestDto> OpenInformationRequests);

public sealed record DecisionDto(DecisionOutcome Outcome, MoneyDto? ApprovedAmount, string Reason, DateTimeOffset DecidedAt);

public sealed record StatusChangeDto(ClaimStatus? FromStatus, ClaimStatus ToStatus, DateTimeOffset ChangedAt);

public sealed record InformationRequestDto(Guid Id, string Question, DateTimeOffset RequestedAt);

/// <summary>
/// One of the claimant's policies, offered when submitting a claim. Product type and currency are what the claim must
/// match; MaxPayable is the coverage limit less the deductible.
/// </summary>
public sealed record PolicySummary(
    string PolicyNumber,
    Market Market,
    ProductType ProductType,
    string Currency,
    MoneyDto MaxPayable,
    DateOnly StartDate,
    DateOnly EndDate);
