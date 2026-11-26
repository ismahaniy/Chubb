using Claims.Application.Auth;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Users;

internal sealed class UserRepository(ClaimsDbContext db) : IUserRepository
{
    public Task<UserCredentials?> FindCredentialsByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users
            .AsNoTracking()
            .Where(u => u.Email == normalizedEmail)
            .Select(u => new UserCredentials(u.Id, u.Email, u.DisplayName, u.Role, u.PasswordHash))
            .SingleOrDefaultAsync(cancellationToken);
}
