using Claims.Api.Auth;
using Claims.Application.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

/// <summary>In-app notifications for the signed-in user (any role). Written asynchronously by the Worker from claim events.</summary>
[ApiController]
[Route("api/notifications")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class NotificationsController(NotificationService notifications) : ControllerBase
{
    /// <summary>The signed-in user's 50 most recent notifications, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationView>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationView>>> Mine(CancellationToken cancellationToken) =>
        Ok(await notifications.ListMineAsync(User.ToActor(), cancellationToken));

    /// <summary>Marks the signed-in user's notifications created at or before <c>upTo</c> as read. Idempotent.</summary>
    [HttpPost("read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> MarkRead(MarkNotificationsReadRequest request, CancellationToken cancellationToken)
    {
        await notifications.MarkReadAsync(User.ToActor(), request, cancellationToken);
        return NoContent();
    }
}
