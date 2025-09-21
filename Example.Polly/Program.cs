using Example.Shared;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services
    .AddHttpClient<SomeServiceClient>()
    .AddPolicyHandler(GetCombinedPolicy());

var app = builder.Build();

// Configure the HTTP request pipeline.
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
return;

static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy()
{
    var policy = HttpPolicyExtensions.HandleTransientHttpError();
    var retry = policy
        .WaitAndRetryAsync(3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (_, _, count, _) => { Console.WriteLine($"Retry number: {count}"); });

    var circuitBreaker = policy
        .CircuitBreakerAsync(3, TimeSpan.FromMinutes(1), 
            onBreak: (_, span) => { Console.WriteLine($"Breaking the circuit for: {span.ToString()}"); },
            onHalfOpen: () => { Console.WriteLine("Circuit in a half-open state, next call is a try..."); },
            onReset: () => { Console.WriteLine("Circuit reset"); });

    var timeout = Policy.TimeoutAsync<HttpResponseMessage>(5);
    
    return Policy.WrapAsync(circuitBreaker, retry, timeout);
}