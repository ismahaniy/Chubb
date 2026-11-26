using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Claims.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = "";

    [Required]
    public string Audience { get; init; } = "";

    /// <summary>HS256 needs at least 256 bits.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; init; } = "";

    [Range(1, 24 * 60)]
    public int LifetimeMinutes { get; init; } = 480;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
