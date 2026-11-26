using Claims.Application.Common;

namespace Claims.Worker;

/// <summary>The Worker writes no claims, so it stages no outbox rows and has no correlation id to copy.</summary>
public sealed class NoCorrelationContext : ICorrelationContext
{
    public string? CorrelationId => null;
}
