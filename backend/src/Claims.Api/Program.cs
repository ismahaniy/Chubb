using Claims.Api.Errors;
using Claims.Api.Observability;
using Claims.Infrastructure;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] = CorrelationIdMiddleware.Get(context.HttpContext));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

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

app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();
