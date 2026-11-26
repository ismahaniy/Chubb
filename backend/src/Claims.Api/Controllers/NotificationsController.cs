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
}
