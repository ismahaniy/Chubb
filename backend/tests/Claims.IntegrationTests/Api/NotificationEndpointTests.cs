using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Application.Auth;
using Claims.Application.Notifications;
using Claims.Domain;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.IntegrationTests.Api;

/// <summary>GET /api/notifications. No Worker runs here, so the test writes notifications straight to the database.</summary>
public sealed class NotificationEndpointTests(ApiFactory factory)
{
    private const string Claimant1 = "claimant1@claimdesk.test";
    private const string Claimant2 = "claimant2@claimdesk.test";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task A_user_sees_only_their_own_notifications()
    {
        var mine = $"for claimant1 {Guid.NewGuid()}";
        var theirs = $"for claimant2 {Guid.NewGuid()}";
        await AddNotificationAsync(Claimant1, mine);
        await AddNotificationAsync(Claimant2, theirs);

        using var client = await LoginAsync(Claimant1);
        var notifications = (await client.GetFromJsonAsync<List<NotificationView>>("/api/notifications", Json, Ct))!;

        notifications.ShouldContain(n => n.Message == mine);
        notifications.ShouldNotContain(n => n.Message == theirs);
        notifications.Select(n => n.CreatedAt).ShouldBeInOrder(SortDirection.Descending);
    }

    [Fact]
    public async Task Mark_read_marks_only_the_callers_notifications_up_to_the_given_time()
    {
        // Times far apart and away from "now", so notifications other tests add cannot fall in between.
        var seen = DateTimeOffset.UtcNow.AddDays(-30);
        var older = await AddNotificationAsync(Claimant1, "older", seen);
        var newer = await AddNotificationAsync(Claimant1, "newer", DateTimeOffset.UtcNow.AddDays(30));
        var theirs = await AddNotificationAsync(Claimant2, "theirs", seen);

        using var client = await LoginAsync(Claimant1);
        using var response = await client.PostAsJsonAsync("/api/notifications/read", new MarkNotificationsReadRequest(seen), Json, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var firstReadAt = await ReadAtAsync(older);
        firstReadAt.ShouldNotBeNull();
        (await ReadAtAsync(newer)).ShouldBeNull();
        (await ReadAtAsync(theirs)).ShouldBeNull();

        // Idempotent: a repeat keeps the first read time.
        using var again = await client.PostAsJsonAsync("/api/notifications/read", new MarkNotificationsReadRequest(seen), Json, Ct);
        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ReadAtAsync(older)).ShouldBe(firstReadAt);
    }

    [Fact]
    public async Task Mark_read_without_upTo_is_400()
    {
        using var client = await LoginAsync(Claimant1);

        using var response = await client.PostAsJsonAsync("/api/notifications/read", new { }, Json, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Without_a_token_it_is_401()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/notifications", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<Guid> AddNotificationAsync(string email, string message, DateTimeOffset? createdAt = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync(Ct);
        var claimId = await db.Claims.Where(c => c.ClaimantId == userId).Select(c => c.Id).FirstAsync(Ct);
        var notification = new Notification(userId, claimId, message, createdAt ?? DateTimeOffset.UtcNow);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(Ct);
        return notification.Id;
    }

    private async Task<DateTimeOffset?> ReadAtAsync(Guid notificationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        return await db.Notifications.Where(n => n.Id == notificationId).Select(n => n.ReadAt).SingleAsync(Ct);
    }

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, DbSeeder.DemoPassword), Json, Ct);
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>(Json, Ct))!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
