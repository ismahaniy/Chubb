namespace Claims.Application.Common;

/// <summary>The caller is authenticated but may not perform this action. Mapped to HTTP 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);
