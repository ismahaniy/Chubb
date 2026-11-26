namespace Claims.Application.Notifications;

public sealed record NotificationView(Guid Id, Guid ClaimId, string Message, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
