using Example.Shared;
using Microsoft.Extensions.Http.Resilience;
using Polly;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services
    .AddHttpClient<SomeServiceClient>()
    .AddResilienceHandler("DefaultPipeline", pipelineBuilder =>
    {
        pipelineBuilder.AddCircuitBreaker<HttpResponseMessage>(new HttpCircuitBreakerStrategyOptions
        {
            MinimumThroughput = 3,
            FailureRatio = 0.99,
            SamplingDuration = TimeSpan.FromMinutes(5),
            BreakDuration = TimeSpan.FromMinutes(1),
            OnOpened = onOpenedArguments =>
            {
                Console.WriteLine($"Breaking the circuit resiliently for: {onOpenedArguments.BreakDuration.ToString()}");
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = onHalfOpenedArguments =>
            {
                Console.WriteLine("Resilient circuit in a half-open state, next call is a try...");
                return ValueTask.CompletedTask;
            },
            OnClosed = onClosedArguments =>
            {
                Console.WriteLine("Resilient circuit reset");
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
                Console.WriteLine($"Retry number: {onRetryArguments.AttemptNumber}");
                return ValueTask.CompletedTask;
            }
        });
        
        pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(5));
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
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