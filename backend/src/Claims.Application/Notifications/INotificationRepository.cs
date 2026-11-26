namespace Claims.Application.Notifications;

public interface INotificationRepository
{
    /// <summary>The recipient's most recent notifications, newest first.</summary>
    Task<IReadOnlyList<NotificationView>> ListForRecipientAsync(Guid recipientId, int limit, CancellationToken cancellationToken);
}
