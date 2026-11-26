using System.ComponentModel.DataAnnotations;
using Claims.Domain;

namespace Claims.Application.Claims;

// Value types are nullable so [Required] can tell "missing" from a default value; the service unwraps them.

public sealed record InformationRequestRequest(
    [Required, MaxLength(2000)] string Question);

public sealed record AssessmentRequest(
    [Required, Range(typeof(decimal), "0.01", "9999999999999999.99")] decimal? ReserveAmount,
    [Required, StringLength(3, MinimumLength = 3)] string Currency,
    [MaxLength(2000)] string? Notes);

/// <summary>Approve with an amount and currency, or reject without them. Both need a reason.</summary>
public sealed record DecisionRequest(
    [Required] DecisionOutcome? Outcome,
    [Range(typeof(decimal), "0.01", "9999999999999999.99")] decimal? ApprovedAmount,
    [StringLength(3, MinimumLength = 3)] string? Currency,
    [Required, MaxLength(2000)] string Reason) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Outcome == DecisionOutcome.Approved)
        {
            if (ApprovedAmount is null)
            {
                yield return new ValidationResult("An approval needs an approved amount.", [nameof(ApprovedAmount)]);
            }

            if (Currency is null)
            {
                yield return new ValidationResult("An approval needs a currency.", [nameof(Currency)]);
            }
        }
        else if (Outcome == DecisionOutcome.Rejected && (ApprovedAmount is not null || Currency is not null))
        {
            yield return new ValidationResult("A rejection has no approved amount or currency.", [nameof(ApprovedAmount)]);
        }
    }
}
