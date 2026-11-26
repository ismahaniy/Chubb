using Claims.Application.Common;

namespace Claims.Application.Notifications;

/// <summary>In-app notifications. Written by the Worker from claim events; read and marked read here.</summary>
public sealed class NotificationService(INotificationRepository notifications, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public const int Limit = 50;

    /// <summary>Always the signed-in user's own notifications: there is no way to ask for someone else's.</summary>
    public Task<IReadOnlyList<NotificationView>> ListMineAsync(Actor actor, CancellationToken cancellationToken) =>
        notifications.ListForRecipientAsync(actor.UserId, Limit, cancellationToken);

    /// <summary>Marks the user's own notifications up to the given time as read. Idempotent: already-read ones keep their first read time.</summary>
    public async Task MarkReadAsync(Actor actor, MarkNotificationsReadRequest request, CancellationToken cancellationToken)
    {
        var unread = await notifications.ListUnreadForUpdateAsync(actor.UserId, request.UpTo!.Value, cancellationToken);
        if (unread.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        foreach (var notification in unread)
        {
            notification.MarkRead(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
