using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Application.Auth;
using Claims.Application.Claims;
using Claims.Domain;
using Claims.Infrastructure.Persistence;

namespace Claims.IntegrationTests.Api;

/// <summary>GET /api/policies/mine. Policies are seeded and never written by other tests, so exact lists are safe.</summary>
public sealed class PolicyEndpointTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task A_claimant_gets_only_their_own_policies_with_currency_and_max_payable()
    {
        using var client = await LoginAsync("claimant1@claimdesk.test");

        var policies = (await client.GetFromJsonAsync<List<PolicySummary>>("/api/policies/mine", Json, Ct))!;

        policies.Select(p => p.PolicyNumber)
            .ShouldBe(["POL-ID-0001", "POL-MY-0001", "POL-MY-0002", "POL-SG-0001", "POL-SG-0002"]);
        var sgMotor = policies.Single(p => p.PolicyNumber == "POL-SG-0001");
        sgMotor.Market.ShouldBe(Market.Singapore);
        sgMotor.ProductType.ShouldBe(ProductType.Motor);
        sgMotor.Currency.ShouldBe("SGD");
        sgMotor.MaxPayable.ShouldBe(new MoneyDto(49_500m, "SGD")); // 50,000 limit less 500 deductible
    }

    [Fact]
    public async Task An_officer_is_forbidden()
    {
        using var client = await LoginAsync("officer1@claimdesk.test");

        using var response = await client.GetAsync("/api/policies/mine", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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
