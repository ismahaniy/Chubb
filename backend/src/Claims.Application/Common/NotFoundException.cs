namespace Claims.Application.Common;

/// <summary>The requested resource does not exist (or the caller may not know it exists). Mapped to HTTP 404.</summary>
public sealed class NotFoundException(string message) : Exception(message);
