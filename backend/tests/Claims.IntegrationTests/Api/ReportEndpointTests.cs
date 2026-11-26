using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Application.Auth;
using Claims.Application.Reports;
using Claims.Infrastructure.Persistence;

namespace Claims.IntegrationTests.Api;

/// <summary>
/// The report endpoints over HTTP. Other test classes add claims to the same database in parallel, so totals are not
/// asserted here; the numbers are covered by ReportRepositoryTests against a database of its own.
/// </summary>
public sealed class ReportEndpointTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Manager_gets_exposure_with_one_line_per_market_product_and_currency()
    {
        using var manager = await LoginAsync("manager@claimdesk.test");

        var report = (await manager.GetFromJsonAsync<ExposureReport>("/api/reports/exposure", Json, Ct))!;

        report.Lines.ShouldNotBeEmpty();
        report.Lines.Select(l => (l.Market, l.ProductType, l.Exposure.Currency)).ShouldBeUnique();
        report.Lines.ShouldAllBe(l => l.OpenClaims > 0 && l.Exposure.Amount > 0);
    }

    [Fact]
    public async Task Manager_gets_workload_for_every_seeded_officer()
    {
        using var manager = await LoginAsync("manager@claimdesk.test");

        var report = (await manager.GetFromJsonAsync<WorkloadReport>("/api/reports/workload", Json, Ct))!;

        report.Officers.Select(o => o.Name).ShouldBe(["Kenji Sato", "Marco Reyes", "Nadia Halim", "Priya Menon"]);
    }

    [Theory]
    [InlineData("officer1@claimdesk.test", "/api/reports/exposure")]
    [InlineData("officer1@claimdesk.test", "/api/reports/workload")]
    [InlineData("claimant1@claimdesk.test", "/api/reports/exposure")]
    [InlineData("claimant1@claimdesk.test", "/api/reports/workload")]
    public async Task Only_managers_can_read_reports(string email, string url)
    {
        using var client = await LoginAsync(email);

        using var response = await client.GetAsync(url, Ct);

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
