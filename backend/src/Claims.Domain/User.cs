namespace Claims.Domain;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public Role Role { get; private set; }
    public string PasswordHash { get; private set; } = null!;

    private User()
    {
    }

    public User(string email, string displayName, Role role, string passwordHash)
    {
        Id = Guid.CreateVersion7();
        Email = Required(email, nameof(email)).ToLowerInvariant();
        DisplayName = Required(displayName, nameof(displayName));
        Role = role;
        PasswordHash = Required(passwordHash, nameof(passwordHash));
    }

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new DomainException($"{name} is required.") : value.Trim();
}
