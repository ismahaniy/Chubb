using Claims.Domain;

namespace Claims.Application.Claims;

public interface IPolicyRepository
{
    /// <summary>Read-only: the policy is only used to validate and snapshot onto a new claim.</summary>
    Task<Policy?> FindByNumberAsync(string policyNumber, CancellationToken cancellationToken);

    /// <summary>The holder's policies, ordered by policy number.</summary>
    Task<IReadOnlyList<PolicySummary>> ListForHolderAsync(Guid holderId, CancellationToken cancellationToken);
}
