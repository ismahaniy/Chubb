using Claims.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Claims.IntegrationTests.Api.ApiFactory))]

namespace Claims.IntegrationTests.Api;

/// <summary>
/// The real API (Development environment, demo JWT key) against throwaway PostgreSQL 17 and Kafka 4.1 containers,
/// started once for the whole test assembly. The API migrates and seeds on start-up, as in docker compose.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly KafkaContainer _kafka = new KafkaBuilder("apache/kafka:4.1.0").Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _kafka.StartAsync());

        // Start the host here, once. WebApplicationFactory starts it lazily and not thread-safely, so parallel
        // test classes calling CreateClient() could boot two hosts that migrate and seed the same database at once.
        StartServer();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
        .UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", _postgres.GetConnectionString())
        .UseSetting(DependencyInjection.KafkaBootstrapServersKey, _kafka.GetBootstrapAddress())
        .UseSetting("Database:MigrateOnStartup", "true");

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _kafka.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
