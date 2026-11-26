using Claims.Application.Auth;
using Claims.Domain;
using Microsoft.AspNetCore.Identity;

namespace Claims.Infrastructure.Security;

/// <summary>ASP.NET Core Identity's PBKDF2 hasher, the same one DbSeeder uses to hash the demo passwords.</summary>
internal sealed class PasswordVerifier : IPasswordVerifier
{
    private static readonly PasswordHasher<User> Hasher = new();

    // Verified against when the user is unknown, so both failure paths cost one PBKDF2 run.
    private static readonly string DummyHash = Hasher.HashPassword(null!, Guid.NewGuid().ToString());

    public bool Verify(string? passwordHash, string password)
    {
        var result = Hasher.VerifyHashedPassword(null!, passwordHash ?? DummyHash, password);
        return passwordHash is not null && result != PasswordVerificationResult.Failed;
    }
}
