using System.ComponentModel.DataAnnotations;

namespace Claims.Application.Notifications;

/// <param name="UpTo">
/// The newest notification time the user has seen (as the API returned it). Later notifications stay unread, so one
/// that arrives while the list is open is not marked read unseen.
/// </param>
public sealed record MarkNotificationsReadRequest([Required] DateTimeOffset? UpTo);
