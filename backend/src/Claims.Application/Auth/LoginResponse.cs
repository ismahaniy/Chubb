using Claims.Domain;

namespace Claims.Application.Auth;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUser User);

public sealed record CurrentUser(Guid Id, string Email, string DisplayName, Role Role);
