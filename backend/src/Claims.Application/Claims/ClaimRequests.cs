using System.ComponentModel.DataAnnotations;
using Claims.Domain;

namespace Claims.Application.Claims;

// Value types are nullable so [Required] can tell "missing" from a default value; the service unwraps them.

public sealed record SubmitClaimRequest(
    [Required, MaxLength(32)] string PolicyNumber,
    [Required] ProductType? ProductType,
    [Required] DateOnly? IncidentDate,
    [Required, MaxLength(2000)] string Description,
    [Required, Range(typeof(decimal), "0.01", "9999999999999999.99")] decimal? ClaimedAmount,
    [Required, StringLength(3, MinimumLength = 3)] string Currency);

public sealed record InformationResponseRequest(
    [Required, MaxLength(2000)] string Answer);
