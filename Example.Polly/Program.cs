using Example.Shared;
using Polly;
using Polly.Extensions.Http;
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
        .AddPolicyHandler(GetCombinedPolicy());

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();

    app.MapGet("polly/users", async (SomeServiceClient client) =>
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

return;

static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy()
{
    var policy = HttpPolicyExtensions.HandleTransientHttpError();
    var retry = policy
        .WaitAndRetryAsync(3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (_, _, count, _) => { Log.Warning("Retry number: {RetryCount}", count); });

    var circuitBreaker = policy
        .CircuitBreakerAsync(3, TimeSpan.FromMinutes(1),
            onBreak: (_, span) => { Log.Warning("Breaking the circuit for: {Span}", span.ToString()); },
            onHalfOpen: () => { Log.Warning("Circuit in a half-open state, next call is a try..."); },
            onReset: () => { Log.Warning("Circuit reset"); });

    var timeout = Policy.TimeoutAsync<HttpResponseMessage>(5);

    return Policy.WrapAsync(circuitBreaker, retry, timeout);
}