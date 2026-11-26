using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Claims.Api.Observability;

/// <summary>
/// Gives every request a correlation id: the caller's X-Correlation-Id when it is well formed, otherwise the
/// W3C trace id, so logs, traces and (later) event envelopes share one id. Echoed in the response header and
/// added to the logging scope.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    private const string ItemKey = "CorrelationId";

    public static string? Get(HttpContext httpContext) => httpContext.Items[ItemKey] as string;

    public async Task InvokeAsync(HttpContext httpContext)
    {
        // A repeated header arrives comma-joined and fails the pattern, like any other malformed value.
        var inbound = httpContext.Request.Headers[HeaderName].ToString();
        var correlationId = AllowedId().IsMatch(inbound)
            ? inbound
            : Activity.Current?.TraceId.ToHexString() ?? httpContext.TraceIdentifier;

        httpContext.Items[ItemKey] = correlationId;
        httpContext.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
        {
            await next(httpContext);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex AllowedId();
}
