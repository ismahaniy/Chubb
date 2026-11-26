extern alias worker;

using System.Text.Json;
using Claims.Application.Common;
using Claims.Infrastructure;
using Claims.Infrastructure.Messaging;
using Claims.Infrastructure.Notifications;
using Claims.Infrastructure.Outbox;
using Claims.Infrastructure.Persistence;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using NotificationProjector = worker::Claims.Worker.NotificationProjector;
using NoCorrelationContext = worker::Claims.Worker.NoCorrelationContext;

namespace Claims.IntegrationTests.Worker;

/// <summary>
/// The real NotificationProjector against throwaway PostgreSQL 17 and Kafka 4.1. Kafka delivers at least once, so the
/// same event can arrive twice (e.g. the outbox publisher crashed after producing but before setting sent_at).
/// </summary>
public sealed class NotificationProjectorTests(WorkerFixture fixture) : IClassFixture<WorkerFixture>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Duplicate_event_creates_one_notification()
    {
        var claimId = await fixture.WithDbAsync(db => db.Claims.AsNoTracking().OrderBy(c => c.ClaimNumber).Select(c => c.Id).FirstAsync(Ct));
        var before = await NotificationCountAsync(claimId);

        var duplicate = AssignedEvent(claimId);
        var sentinel = AssignedEvent(claimId);

        // Same key, so one partition, consumed in this order: once the sentinel is processed, both copies have been.
        await fixture.ProduceAsync(duplicate);
        await fixture.ProduceAsync(duplicate);
        await fixture.ProduceAsync(sentinel);
        await WaitUntilProcessedAsync(sentinel.EventId);

        (await NotificationCountAsync(claimId)).ShouldBe(before + 2); // the duplicate once, the sentinel once
        (await fixture.WithDbAsync(db => db.ProcessedEvents.CountAsync(e => e.EventId == duplicate.EventId, Ct))).ShouldBe(1);
    }

    private static EventEnvelope AssignedEvent(Guid claimId) => EventEnvelope.From(new OutboxMessage
    {
        Id = Guid.CreateVersion7(),
        EventType = "ClaimAssigned",
        ClaimId = claimId,
        CorrelationId = "projector-test",
        Payload = JsonSerializer.Serialize(new { officerId = Guid.NewGuid() }),
        OccurredAt = DateTimeOffset.UtcNow,
    });

    private Task<int> NotificationCountAsync(Guid claimId) =>
        fixture.WithDbAsync(db => db.Notifications.CountAsync(n => n.ClaimId == claimId, Ct));

    private async Task WaitUntilProcessedAsync(Guid eventId)
    {
        var deadline = DateTimeOffset.UtcNow + Timeout;
        while (!await fixture.WithDbAsync(db => db.ProcessedEvents.AnyAsync(e => e.EventId == eventId, Ct)))
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"Event {eventId} was not processed within {Timeout}.");
            }

            await Task.Delay(200, Ct);
        }
    }
}

/// <summary>
/// PostgreSQL (migrated and seeded) + Kafka with the compose topics, and a host running only the NotificationProjector,
/// registered as the Worker registers it. The test plays the outbox publisher by producing envelopes itself.
/// </summary>
public sealed class WorkerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly KafkaContainer _kafka = new KafkaBuilder("apache/kafka:4.1.0").Build();
    private IHost _host = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _kafka.StartAsync());

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = _postgres.GetConnectionString(),
            [DependencyInjection.KafkaBootstrapServersKey] = _kafka.GetBootstrapAddress(),
        });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ICorrelationContext, NoCorrelationContext>();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddHostedService<NotificationProjector>();
        _host = builder.Build();

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Database.MigrateAsync();
        }

        await _host.Services.GetRequiredService<IAdminClient>().CreateTopicsAsync(
        [
            new TopicSpecification { Name = KafkaTopics.Events, NumPartitions = 6, ReplicationFactor = 1 },
            new TopicSpecification { Name = KafkaTopics.DeadLetter, NumPartitions = 6, ReplicationFactor = 1 },
        ]);

        await _host.StartAsync();
    }

    public Task ProduceAsync(EventEnvelope envelope) =>
        _host.Services.GetRequiredService<IProducer<string, string>>().ProduceAsync(
            KafkaTopics.Events,
            new Message<string, string> { Key = envelope.ClaimId.ToString(), Value = envelope.ToJson() });

    public async Task<T> WithDbAsync<T>(Func<ClaimsDbContext, Task<T>> work)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<ClaimsDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _kafka.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
