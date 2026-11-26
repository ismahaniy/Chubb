namespace Claims.Domain;

public sealed class Notification
{
    public Guid Id { get; private set; }
    public Guid RecipientId { get; private set; }
    public Guid ClaimId { get; private set; }
    public string Message { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    private Notification()
    {
    }

    public Notification(Guid recipientId, Guid claimId, string message, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new DomainException("Notification message is required.");
        }

        Id = Guid.CreateVersion7();
        RecipientId = recipientId;
        ClaimId = claimId;
        Message = message.Trim();
        CreatedAt = createdAt.ToUniversalTime();
    }

    /// <summary>Idempotent: the first read time is kept.</summary>
    public void MarkRead(DateTimeOffset now) => ReadAt ??= now.ToUniversalTime();
}
