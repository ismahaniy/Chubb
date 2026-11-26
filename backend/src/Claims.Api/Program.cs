using System.Reflection;
using System.Text.Json.Serialization;
using Claims.Api.Auth;
using Claims.Api.Errors;
using Claims.Api.Observability;
using Claims.Api.OpenApi;
using Claims.Application.Auth;
using Claims.Application.Claims;
using Claims.Application.Common;
using Claims.Application.Notifications;
using Claims.Application.Reports;
using Claims.Infrastructure;
using Claims.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Build-time OpenAPI generation runs this program (and starts the host) without real configuration.
// Documented way to detect it: https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi#customizing-run-time-behavior-during-build-time-document-generation
var isGeneratingOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

// Enums as strings for controllers and for everything else (OpenAPI schemas, /health writer).
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] = CorrelationIdMiddleware.Get(context.HttpContext));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi(o => o
    .AddDocumentTransformer<BearerSecurityTransformer>()
    .AddOperationTransformer<BearerSecurityTransformer>());

var jwtOptions = builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations();
if (!isGeneratingOpenApi)
{
    jwtOptions.ValidateOnStart(); // a missing or short signing key stops the API at start-up, not at first login
}
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorizationBuilder().AddClaimsPolicies();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAccessTokenIssuer, JwtTokenIssuer>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ClaimantClaimService>();
builder.Services.AddScoped<OfficerClaimService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Opt-in: local dev turns this on via launchSettings; elsewhere migrations are applied deliberately.
// MigrateAsync also runs the idempotent seeder (UseAsyncSeeding).
if (!isGeneratingOpenApi && app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthReport }).AllowAnonymous();

await app.RunAsync();

static Task WriteHealthReport(HttpContext context, HealthReport report) =>
    context.Response.WriteAsJsonAsync(new
    {
        status = report.Status,
        checks = report.Entries.ToDictionary(
            e => e.Key,
            e => new { status = e.Value.Status, description = e.Value.Description, durationMs = (int)e.Value.Duration.TotalMilliseconds }),
    });
