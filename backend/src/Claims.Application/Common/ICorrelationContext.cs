namespace Claims.Application.Common;

/// <summary>The correlation id of the current unit of work (HTTP request or consumed message), copied onto outbox rows.</summary>
public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
