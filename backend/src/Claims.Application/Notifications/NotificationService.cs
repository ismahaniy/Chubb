using Claims.Application.Common;

namespace Claims.Application.Notifications;

/// <summary>In-app notifications. Written by the Worker from claim events; read here.</summary>
public sealed class NotificationService(INotificationRepository notifications)
{
    public const int Limit = 50;

    /// <summary>Always the signed-in user's own notifications: there is no way to ask for someone else's.</summary>
    public Task<IReadOnlyList<NotificationView>> ListMineAsync(Actor actor, CancellationToken cancellationToken) =>
        notifications.ListForRecipientAsync(actor.UserId, Limit, cancellationToken);
}
