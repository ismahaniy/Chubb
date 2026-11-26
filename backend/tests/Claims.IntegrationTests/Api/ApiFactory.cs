using Claims.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Claims.IntegrationTests.Api.ApiFactory))]

namespace Claims.IntegrationTests.Api;

/// <summary>
/// The real API (Development environment) against a throwaway PostgreSQL 17 container,
/// started once for the whole test assembly. The API migrates and seeds on start-up, as in docker compose.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Start the host here, once. WebApplicationFactory starts it lazily and not thread-safely, so parallel
        // test classes calling CreateClient() could boot two hosts that migrate and seed the same database at once.
        StartServer();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
        .UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", _postgres.GetConnectionString())
        .UseSetting("Database:MigrateOnStartup", "true");

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
