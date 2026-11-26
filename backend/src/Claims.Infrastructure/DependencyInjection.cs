using Claims.Application.Auth;
using Claims.Application.Claims;
using Claims.Application.Common;
using Claims.Application.Reports;
using Claims.Infrastructure.Claims;
using Claims.Infrastructure.Health;
using Claims.Infrastructure.Outbox;
using Claims.Infrastructure.Persistence;
using Claims.Infrastructure.Policies;
using Claims.Infrastructure.Reports;
using Claims.Infrastructure.Security;
using Claims.Infrastructure.Users;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Claims.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Claims";
    public const string KafkaBootstrapServersKey = "Kafka:BootstrapServers";

    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(3);

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string 'ConnectionStrings:{ConnectionStringName}' is not configured.");

        services.AddDbContext<ClaimsDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .UseAsyncSeeding(DbSeeder.SeedAsync));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPolicyRepository, PolicyRepository>();
        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IOutbox, EfOutbox>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IPasswordVerifier, PasswordVerifier>();

        // One long-lived admin client (librdkafka reconnects by itself); built on first use, disposed by the container.
        services.AddSingleton<IAdminClient>(sp =>
        {
            var bootstrapServers = sp.GetRequiredService<IConfiguration>()[KafkaBootstrapServersKey]
                ?? throw new InvalidOperationException($"'{KafkaBootstrapServersKey}' is not configured.");
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Kafka");
            return new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers })
                .SetLogHandler((_, message) => logger.Log(
                    (LogLevel)message.LevelAs(LogLevelType.MicrosoftExtensionsLogging), "{Facility}: {Message}", message.Facility, message.Message))
                .Build();
        });

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", timeout: HealthCheckTimeout)
            .AddCheck<KafkaHealthCheck>("kafka", timeout: HealthCheckTimeout);

        return services;
    }
}
