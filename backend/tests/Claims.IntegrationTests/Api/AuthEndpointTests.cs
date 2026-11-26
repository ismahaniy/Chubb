using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Claims.Api.Auth;
using Claims.Application.Auth;
using Claims.Domain;
using Claims.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.IntegrationTests.Api;

public sealed class AuthEndpointTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Seeded_user_can_log_in_and_the_token_identifies_them()
    {
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("officer1@claimdesk.test", DbSeeder.DemoPassword), Json, Ct);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await login.Content.ReadFromJsonAsync<LoginResponse>(Json, Ct))!;
        body.User.Role.ShouldBe(Role.Officer);
        body.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        var me = await client.GetFromJsonAsync<CurrentUser>("/api/auth/me", Json, Ct);
        me.ShouldBe(body.User);
    }

    [Fact]
    public async Task Wrong_password_is_401_problem_details_with_correlation_id()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("officer1@claimdesk.test", "not-the-password"), Json, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("status").GetInt32().ShouldBe(401);
        problem.GetProperty("correlationId").GetString().ShouldBe(response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Missing_email_is_400_validation_problem()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { password = "x" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").TryGetProperty("Email", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Protected_endpoint_without_token_is_401_problem_details()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData(Policies.Claimant, Role.Claimant, true)]
    [InlineData(Policies.Claimant, Role.Officer, false)]
    [InlineData(Policies.Officer, Role.Officer, true)]
    [InlineData(Policies.Officer, Role.Manager, false)]
    [InlineData(Policies.Manager, Role.Manager, true)]
    [InlineData(Policies.Manager, Role.Claimant, false)]
    [InlineData(Policies.Staff, Role.Officer, true)]
    [InlineData(Policies.Staff, Role.Manager, true)]
    [InlineData(Policies.Staff, Role.Claimant, false)]
    public async Task Role_policies_admit_only_their_roles(string policy, Role role, bool allowed)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new System.Security.Claims.Claim(TokenClaims.Role, role.ToString())], "test", TokenClaims.Name, TokenClaims.Role));
        var authorization = factory.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(user, policy);

        result.Succeeded.ShouldBe(allowed);
    }
}
