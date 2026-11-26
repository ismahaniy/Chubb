using System.Text.Json.Serialization;
using Claims.Api.Auth;
using Claims.Api.Errors;
using Claims.Api.Observability;
using Claims.Application.Auth;
using Claims.Infrastructure;
using Claims.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Enums as strings for controllers and for minimal endpoints.
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] = CorrelationIdMiddleware.Get(context.HttpContext));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart(); // a missing or short signing key stops the API at start-up, not at first login
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorizationBuilder().AddClaimsPolicies();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAccessTokenIssuer, JwtTokenIssuer>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Opt-in: local dev turns this on via launchSettings; elsewhere migrations are applied deliberately.
// MigrateAsync also runs the idempotent seeder (UseAsyncSeeding).
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
    await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(); // empty 401/403/404 responses get a ProblemDetails body too
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

await app.RunAsync();
