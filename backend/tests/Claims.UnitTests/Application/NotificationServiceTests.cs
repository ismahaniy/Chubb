using Claims.Application.Common;
using Claims.Application.Notifications;
using Claims.Domain;
using NSubstitute;

namespace Claims.UnitTests.Application;

public sealed class NotificationServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Actor User = new(Guid.CreateVersion7(), Role.Claimant);

    private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly NotificationService _service;

    public NotificationServiceTests() =>
        _service = new NotificationService(_notifications, _unitOfWork, new FixedClock(Now));

    [Fact]
    public async Task Mark_read_marks_the_users_notifications_up_to_the_given_time_and_saves()
    {
        var upTo = Now.AddMinutes(-5);
        var unread = new Notification(User.UserId, Guid.CreateVersion7(), "We received your claim.", upTo);
        _notifications.ListUnreadForUpdateAsync(User.UserId, upTo, Arg.Any<CancellationToken>()).Returns([unread]);

        await _service.MarkReadAsync(User, new MarkNotificationsReadRequest(upTo), Ct);

        unread.ReadAt.ShouldBe(Now);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Mark_read_with_nothing_unread_saves_nothing()
    {
        _notifications.ListUnreadForUpdateAsync(User.UserId, Now, Arg.Any<CancellationToken>()).Returns([]);

        await _service.MarkReadAsync(User, new MarkNotificationsReadRequest(Now), Ct);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
