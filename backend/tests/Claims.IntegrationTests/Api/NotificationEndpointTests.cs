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
    public async Task Without_a_token_it_is_401()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/notifications", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task AddNotificationAsync(string email, string message)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync(Ct);
        var claimId = await db.Claims.Where(c => c.ClaimantId == userId).Select(c => c.Id).FirstAsync(Ct);
        db.Notifications.Add(new Notification(userId, claimId, message, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(Ct);
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
