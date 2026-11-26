using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Persistence;

/// <summary>Claim numbers come from a PostgreSQL sequence, formatted CLM-000001.</summary>
public static class ClaimNumberSequence
{
    public const string Name = "claim_number_seq";

    private const string NextValueSql = $"SELECT nextval('{Name}') AS \"Value\"";

    public static string Format(long value) => $"CLM-{value:D6}";

    public static async Task<string> NextAsync(ClaimsDbContext db, CancellationToken cancellationToken)
    {
        var value = await db.Database
            .SqlQueryRaw<long>(NextValueSql)
            .SingleAsync(cancellationToken);
        return Format(value);
    }
}
