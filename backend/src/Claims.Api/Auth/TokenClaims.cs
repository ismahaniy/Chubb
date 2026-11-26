namespace Claims.Api.Auth;

/// <summary>Claim names in our tokens. Inbound claim mapping is off, so these arrive unchanged.</summary>
public static class TokenClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}
