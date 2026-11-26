using Claims.Application.Common;
using Claims.Infrastructure;
using Claims.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
// AddInfrastructure registers the API's outbox writer, which needs a correlation context. The Worker never writes
// claims, but without this the Development-mode dependency check fails at start-up.
builder.Services.AddSingleton<ICorrelationContext, NoCorrelationContext>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OutboxPublisher>();

var host = builder.Build();

await host.RunAsync();
