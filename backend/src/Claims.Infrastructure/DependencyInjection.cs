using Claims.Application.Auth;
using Claims.Application.Claims;
using Claims.Application.Common;
using Claims.Application.Reports;
using Claims.Infrastructure.Claims;
using Claims.Infrastructure.Health;
using Claims.Infrastructure.Notifications;
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

        // Worker side (the API never resolves these: it does not produce to Kafka).
        services.AddScoped<OutboxDispatcher>();
        services.AddScoped<NotificationProjection>();

        // Long-lived Kafka clients (librdkafka reconnects by itself); built on first use, disposed by the container.
        services.AddSingleton<IAdminClient>(sp =>
            new AdminClientBuilder(new AdminClientConfig { BootstrapServers = BootstrapServers(sp) })
                .SetLogHandler((_, message) => LogKafka(sp, message))
                .Build());

        // One producer for the outbox and the dead-letter topic. Acks=All + idempotence: no loss, and no duplicates
        // or reordering from the client's own retries. A short message timeout makes an outage fail fast, so the
        // publisher releases its row locks and backs off instead of waiting librdkafka's default five minutes.
        services.AddSingleton<IProducer<string, string>>(sp =>
            new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = BootstrapServers(sp),
                Acks = Acks.All,
                EnableIdempotence = true,
                MessageTimeoutMs = 5_000,
            })
                .SetLogHandler((_, message) => LogKafka(sp, message))
                .Build());

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", timeout: HealthCheckTimeout)
            .AddCheck<KafkaHealthCheck>("kafka", timeout: HealthCheckTimeout);

        return services;
    }

    public static string BootstrapServers(IServiceProvider services) =>
        services.GetRequiredService<IConfiguration>()[KafkaBootstrapServersKey]
            ?? throw new InvalidOperationException($"'{KafkaBootstrapServersKey}' is not configured.");

    /// <summary>librdkafka's own log lines, at their own level, under the "Kafka" category.</summary>
    public static void LogKafka(IServiceProvider services, LogMessage message) =>
        services.GetRequiredService<ILoggerFactory>().CreateLogger("Kafka").Log(
            (LogLevel)message.LevelAs(LogLevelType.MicrosoftExtensionsLogging), "{Facility}: {Message}", message.Facility, message.Message);
}
