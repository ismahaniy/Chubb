namespace Claims.Application.Auth;

public interface IPasswordVerifier
{
    /// <summary>Verifies against the stored hash. A null hash still costs one verification (no timing hint that the user is unknown).</summary>
    bool Verify(string? passwordHash, string password);
}
