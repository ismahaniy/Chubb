using Claims.Domain;

namespace Claims.Application.Auth;

public sealed record UserCredentials(Guid Id, string Email, string DisplayName, Role Role, string PasswordHash);

public interface IUserRepository
{
    /// <param name="normalizedEmail">Trimmed and lower-cased, as stored.</param>
    Task<UserCredentials?> FindCredentialsByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
}
