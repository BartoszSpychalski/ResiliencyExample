using Example.Shared;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddOpenApi();
    builder.Services
        .AddHttpClient<SomeServiceClient>()
        .AddResilienceHandler("DefaultPipeline", pipelineBuilder =>
        {
            pipelineBuilder.AddCircuitBreaker<HttpResponseMessage>(new HttpCircuitBreakerStrategyOptions
            {
                MinimumThroughput = 3,
                FailureRatio = 0.75,
                SamplingDuration = TimeSpan.FromMinutes(5),
                BreakDuration = TimeSpan.FromMinutes(1),
                OnOpened = onOpenedArguments =>
                {
                    Log.Warning(
                        "Breaking the circuit for: {BreakDuration}", onOpenedArguments.BreakDuration.ToString());
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = onHalfOpenedArguments =>
                {
                    Log.Warning("Circuit in a half-open state, next call is a try...");
                    return ValueTask.CompletedTask;
                },
                OnClosed = onClosedArguments =>
                {
                    Log.Warning("Circuit reset");
                    return ValueTask.CompletedTask;
                }
            });

            pipelineBuilder.AddRetry<HttpResponseMessage>(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                OnRetry = onRetryArguments =>
                {
                    Log.Warning("Retry number: {AttemptNumber}", onRetryArguments.AttemptNumber);
                    return ValueTask.CompletedTask;
                }
            });

            pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(5));
        });

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();

    app.MapGet("resilience/users", async (SomeServiceClient client) =>
        {
            var users = await client.BrowseAsync();
            return users;
        })
        .WithName("GetUsers");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Something went terribly wrong...");
}
finally
{
    Log.CloseAndFlush();
}