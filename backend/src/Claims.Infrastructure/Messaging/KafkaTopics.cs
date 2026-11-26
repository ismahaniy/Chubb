namespace Claims.Infrastructure.Messaging;

/// <summary>Topic and group names. The topics are created by the compose init container (auto-creation is off).</summary>
public static class KafkaTopics
{
    public const string Events = "claims.events";
    public const string DeadLetter = "claims.events.dlq";
    public const string ConsumerGroup = "claims-worker";
}
