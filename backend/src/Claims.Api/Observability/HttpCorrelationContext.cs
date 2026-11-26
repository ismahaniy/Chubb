using Claims.Application.Common;

namespace Claims.Api.Observability;

/// <summary>The correlation id CorrelationIdMiddleware assigned to the current request.</summary>
internal sealed class HttpCorrelationContext(IHttpContextAccessor accessor) : ICorrelationContext
{
    public string? CorrelationId => accessor.HttpContext is { } httpContext ? CorrelationIdMiddleware.Get(httpContext) : null;
}
