namespace Claims.Domain;

/// <summary>
/// Aggregate root. Owns its status: it changes only through the command methods below.
/// Every command checks all of its rules before mutating, so a rejected command leaves the claim untouched.
/// Who may call a command (assigned officer, owning claimant) is authorization and is checked by the service.
/// </summary>
public sealed class Claim
{
    private readonly List<InformationRequest> _informationRequests = [];
    private readonly List<ClaimStatusHistory> _statusHistory = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public Guid PolicyId { get; private set; }
    public Guid ClaimantId { get; private set; }
    public Market Market { get; private set; }
    public ProductType ProductType { get; private set; }
    public DateOnly IncidentDate { get; private set; }
    public string Description { get; private set; } = null!;
    public Money ClaimedAmount { get; private set; } = null!;

    /// <summary>Coverage limit minus deductible, fixed from the policy when the claim was submitted.</summary>
    public Money MaxPayable { get; private set; } = null!;

    public ClaimStatus Status { get; private set; }
    public Guid? AssignedOfficerId { get; private set; }
    public Money? Reserve { get; private set; }
    public string? AssessmentNotes { get; private set; }
    public ClaimDecision? Decision { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }

    public IReadOnlyList<InformationRequest> InformationRequests => _informationRequests;
    public IReadOnlyList<ClaimStatusHistory> StatusHistory => _statusHistory;
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public string Currency => ClaimedAmount.Currency;

    private Claim()
    {
    }

    public static Claim Submit(
        Policy policy,
        Guid claimantId,
        DateOnly incidentDate,
        string description,
        Money claimedAmount,
        DateTimeOffset now)
    {
        if (policy.HolderId != claimantId)
        {
            throw new DomainException("Only the policy holder can submit a claim on this policy.");
        }

        if (!policy.Covers(incidentDate))
        {
            throw new DomainException("The policy was not in force on the incident date.");
        }

        if (claimedAmount.Currency != policy.Currency)
        {
            throw new DomainException($"Claimed amount must be in the policy currency {policy.Currency}.");
        }

        if (claimedAmount.IsZero)
        {
            throw new DomainException("Claimed amount must be greater than zero.");
        }

        var claim = new Claim
        {
            Id = Guid.CreateVersion7(),
            PolicyId = policy.Id,
            ClaimantId = claimantId,
            Market = policy.Market,
            ProductType = policy.ProductType,
            IncidentDate = incidentDate,
            Description = Required(description, nameof(description)),
            ClaimedAmount = claimedAmount,
            MaxPayable = policy.MaxPayable,
            SubmittedAt = now.ToUniversalTime(),
        };

        claim._statusHistory.Add(new ClaimStatusHistory(claim.Id, null, ClaimStatus.Submitted, claimantId, claim.SubmittedAt));
        claim.Status = ClaimStatus.Submitted;
        claim._domainEvents.Add(new ClaimSubmitted(claim.Id, claim.SubmittedAt, claimantId, policy.Id, claimedAmount));
        return claim;
    }

    public void Assign(Guid officerId, DateTimeOffset now)
    {
        EnsureStatus(ClaimStatus.Submitted, "assigned");
        now = now.ToUniversalTime();

        AssignedOfficerId = officerId;
        MoveTo(ClaimStatus.UnderReview, officerId, now);
        _domainEvents.Add(new ClaimAssigned(Id, now, officerId));
    }

    public void RequestInformation(Guid officerId, string question, DateTimeOffset now)
    {
        EnsureStatus(ClaimStatus.UnderReview, "sent an information request");
        question = Required(question, nameof(question));
        now = now.ToUniversalTime();

        var request = new InformationRequest(Id, question, officerId, now);
        _informationRequests.Add(request);
        MoveTo(ClaimStatus.AwaitingInformation, officerId, now);
        _domainEvents.Add(new InformationRequested(Id, now, request.Id, officerId, question));
    }

    public void ProvideInformation(Guid claimantId, string answer, DateTimeOffset now)
    {
        EnsureStatus(ClaimStatus.AwaitingInformation, "given information");
        answer = Required(answer, nameof(answer));
        now = now.ToUniversalTime();

        // The state machine guarantees exactly one open request while AwaitingInformation.
        var request = _informationRequests.Single(r => r.IsOpen);
        request.Answered(answer, now);
        MoveTo(ClaimStatus.UnderReview, claimantId, now);
        _domainEvents.Add(new InformationProvided(Id, now, request.Id, claimantId));
    }

    /// <summary>Records or updates the reserve. Not a status transition.</summary>
    public void RecordAssessment(Guid officerId, Money reserve, string? notes, DateTimeOffset now)
    {
        EnsureStatus(ClaimStatus.UnderReview, "assessed");
        EnsureClaimCurrency(reserve, "Reserve");
        if (reserve.IsZero)
        {
            throw new DomainException("Reserve must be greater than zero.");
        }

        now = now.ToUniversalTime();

        Reserve = reserve;
        AssessmentNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        _domainEvents.Add(new ClaimAssessed(Id, now, officerId, reserve));
    }

    public void Approve(Guid officerId, Money amount, string reason, DateTimeOffset now)
    {
        EnsureStatus(ClaimStatus.UnderReview, "approved");
        EnsureReserveRecorded();
        EnsureClaimCurrency(amount, "Approved amount");
        if (amount.IsZero)
        {
            throw new DomainException("Approved amount must be greater than zero.");
        }

        if (amount.IsGreaterThan(MaxPayable))
        {
            throw new DomainException($"Approved amount {amount} exceeds the maximum payable {MaxPayable} (coverage limit minus deductible).");
        }

        reason = Required(reason, nameof(reason));
        now = now.ToUniversalTime();

        Decision = new ClaimDecision(Id, DecisionOutcome.Approved, amount, reason, officerId, now);
        MoveTo(ClaimStatus.Approved, officerId, now);
        _domainEvents.Add(new ClaimApproved(Id, now, officerId, amount));
    }

    public void Reject(Guid officerId, string reason, DateTimeOffset now)
    {
        EnsureStatus(ClaimStatus.UnderReview, "rejected");
        EnsureReserveRecorded();
        reason = Required(reason, nameof(reason));
        now = now.ToUniversalTime();

        Decision = new ClaimDecision(Id, DecisionOutcome.Rejected, null, reason, officerId, now);
        MoveTo(ClaimStatus.Rejected, officerId, now);
        _domainEvents.Add(new ClaimRejected(Id, now, officerId, reason));
    }

    public void Settle(Guid officerId, DateTimeOffset now)
    {
        EnsureStatus(ClaimStatus.Approved, "settled");
        now = now.ToUniversalTime();

        // Approved status is only reachable through Approve, which always sets an approved amount.
        var amount = Decision!.ApprovedAmount!;
        MoveTo(ClaimStatus.Settled, officerId, now);
        _domainEvents.Add(new ClaimSettled(Id, now, officerId, amount));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void MoveTo(ClaimStatus target, Guid actorId, DateTimeOffset now)
    {
        _statusHistory.Add(new ClaimStatusHistory(Id, Status, target, actorId, now));
        Status = target;
    }

    private void EnsureStatus(ClaimStatus required, string action)
    {
        if (Status != required)
        {
            throw new DomainException($"A claim that is {Status} cannot be {action}; it must be {required}.");
        }
    }

    private void EnsureReserveRecorded()
    {
        if (Reserve is null)
        {
            throw new DomainException("A reserve must be recorded before a decision.");
        }
    }

    private void EnsureClaimCurrency(Money money, string what)
    {
        if (money.Currency != Currency)
        {
            throw new DomainException($"{what} must be in the claim currency {Currency}.");
        }
    }

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new DomainException($"{name} is required.") : value.Trim();
}
