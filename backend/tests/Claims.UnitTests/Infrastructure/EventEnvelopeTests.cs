using System.Text.Json;
using Claims.Domain;
using Claims.Infrastructure.Messaging;
using Claims.Infrastructure.Outbox;
using static Claims.UnitTests.Domain.ClaimTestData;

namespace Claims.UnitTests.Infrastructure;

public sealed class EventEnvelopeTests
{
    [Fact]
    public void Envelope_has_the_contract_fields_and_the_payload_as_an_object()
    {
        var message = OutboxMessageFactory.Create(new ClaimAssigned(Guid.NewGuid(), Now, OfficerId), "corr-1");

        using var json = JsonDocument.Parse(EventEnvelope.From(message).ToJson());
        var root = json.RootElement;

        root.EnumerateObject().Select(p => p.Name)
            .ShouldBe(["eventId", "eventType", "version", "occurredAt", "claimId", "correlationId", "payload"], ignoreOrder: true);
        root.GetProperty("eventId").GetGuid().ShouldBe(message.Id);
        root.GetProperty("eventType").GetString().ShouldBe(nameof(ClaimAssigned));
        root.GetProperty("version").GetInt32().ShouldBe(1);
        root.GetProperty("claimId").GetGuid().ShouldBe(message.ClaimId);
        root.GetProperty("correlationId").GetString().ShouldBe("corr-1");
        root.GetProperty("payload").GetProperty("officerId").GetGuid().ShouldBe(OfficerId);
    }

    [Fact]
    public void Envelope_round_trips()
    {
        var message = OutboxMessageFactory.Create(new ClaimAssigned(Guid.NewGuid(), Now, OfficerId), null);
        var original = EventEnvelope.From(message);

        var parsed = EventEnvelope.Parse(original.ToJson());

        parsed.EventId.ShouldBe(original.EventId);
        parsed.EventType.ShouldBe(original.EventType);
        parsed.ClaimId.ShouldBe(original.ClaimId);
        parsed.OccurredAt.ShouldBe(original.OccurredAt);
        parsed.CorrelationId.ShouldBeNull();
        parsed.Payload.GetProperty("officerId").GetGuid().ShouldBe(OfficerId);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"eventId":"0196f3a0-0000-7000-8000-000000000001","eventType":"ClaimAssigned","claimId":"0196f3a0-0000-7000-8000-000000000002","payload":"not an object"}""")]
    public void Malformed_envelope_is_rejected(string json) =>
        Should.Throw<JsonException>(() => EventEnvelope.Parse(json));
}
