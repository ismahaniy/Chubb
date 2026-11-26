using Claims.Application.Notifications;
using Claims.Domain;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Notifications;

internal sealed class NotificationRepository(ClaimsDbContext db) : INotificationRepository
{
    public async Task<IReadOnlyList<NotificationView>> ListForRecipientAsync(Guid recipientId, int limit, CancellationToken cancellationToken) =>
        await db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientId == recipientId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(limit)
            .Select(n => new NotificationView(n.Id, n.ClaimId, n.Message, n.CreatedAt, n.ReadAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListUnreadForUpdateAsync(Guid recipientId, DateTimeOffset upTo, CancellationToken cancellationToken) =>
        await db.Notifications
            .Where(n => n.RecipientId == recipientId && n.ReadAt == null && n.CreatedAt <= upTo)
            .ToListAsync(cancellationToken);
}
