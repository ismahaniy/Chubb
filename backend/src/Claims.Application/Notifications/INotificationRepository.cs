using Claims.Domain;

namespace Claims.Application.Notifications;

public interface INotificationRepository
{
    /// <summary>The recipient's most recent notifications, newest first.</summary>
    Task<IReadOnlyList<NotificationView>> ListForRecipientAsync(Guid recipientId, int limit, CancellationToken cancellationToken);

    /// <summary>Tracked, for marking read: the recipient's unread notifications created at or before <paramref name="upTo"/>.</summary>
    Task<IReadOnlyList<Notification>> ListUnreadForUpdateAsync(Guid recipientId, DateTimeOffset upTo, CancellationToken cancellationToken);
}
