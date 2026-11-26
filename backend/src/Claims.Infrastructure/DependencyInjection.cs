using Claims.Application.Auth;
using Claims.Infrastructure.Persistence;
using Claims.Infrastructure.Security;
using Claims.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Claims";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string 'ConnectionStrings:{ConnectionStringName}' is not configured.");

        services.AddDbContext<ClaimsDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .UseAsyncSeeding(DbSeeder.SeedAsync));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordVerifier, PasswordVerifier>();

        return services;
    }
}
