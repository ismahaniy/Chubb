namespace Claims.Domain;

public sealed class InformationRequest
{
    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public string Question { get; private set; } = null!;
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public string? Answer { get; private set; }
    public DateTimeOffset? AnsweredAt { get; private set; }

    public bool IsOpen => AnsweredAt is null;

    private InformationRequest()
    {
    }

    internal InformationRequest(Guid claimId, string question, Guid requestedBy, DateTimeOffset requestedAt)
    {
        Id = Guid.CreateVersion7();
        ClaimId = claimId;
        Question = question;
        RequestedBy = requestedBy;
        RequestedAt = requestedAt;
    }

    internal void Answered(string answer, DateTimeOffset answeredAt)
    {
        Answer = answer;
        AnsweredAt = answeredAt;
    }
}
