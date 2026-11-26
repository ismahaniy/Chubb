using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Claims.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Claims.IntegrationTests.Api;

public sealed class HealthEndpointTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Healthy_when_database_and_kafka_are_reachable()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("checks").GetProperty("database").GetProperty("status").GetString().ShouldBe("Healthy");
        body.GetProperty("checks").GetProperty("kafka").GetProperty("status").GetString().ShouldBe("Healthy");
    }

    [Fact]
    public async Task Unhealthy_when_dependencies_are_unreachable()
    {
        // Port 1 on loopback: nothing listens, so both connections are refused quickly. No containers needed.
        await using var unreachable = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2")
            .UseSetting(DependencyInjection.KafkaBootstrapServersKey, "127.0.0.1:1"));
        using var client = unreachable.CreateClient();

        var response = await client.GetAsync("/health", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var checks = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("checks");
        checks.GetProperty("database").GetProperty("status").GetString().ShouldBe("Unhealthy");
        checks.GetProperty("kafka").GetProperty("status").GetString().ShouldBe("Unhealthy");
    }
}
