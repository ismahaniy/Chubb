using Claims.Domain;

namespace Claims.Application.Common;

/// <summary>The signed-in user a use case runs for, taken from the access token.</summary>
public sealed record Actor(Guid UserId, Role Role);
