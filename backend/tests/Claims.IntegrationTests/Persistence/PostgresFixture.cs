using Claims.Infrastructure;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Claims.IntegrationTests.Persistence;

/// <summary>
/// One throwaway PostgreSQL 17 per test class, migrated and seeded exactly as the API does it
/// (same AddInfrastructure registration, same MigrateAsync + UseAsyncSeeding path).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private ServiceProvider _services = null!;
    private AsyncServiceScope _scope;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = _container.GetConnectionString(),
            })
            .Build();

        _services = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider(validateScopes: true);
        _scope = _services.CreateAsyncScope();

        await MigrateAsync(CancellationToken.None);
    }

    /// <summary>A new, independent context (its own change tracker), like one per HTTP request.</summary>
    public ClaimsDbContext CreateDbContext() =>
        new(_scope.ServiceProvider.GetRequiredService<DbContextOptions<ClaimsDbContext>>());

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _services.DisposeAsync();
        await _container.DisposeAsync();
    }
}
