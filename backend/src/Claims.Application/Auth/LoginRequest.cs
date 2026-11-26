using System.ComponentModel.DataAnnotations;

namespace Claims.Application.Auth;

public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(128)] string Password);
