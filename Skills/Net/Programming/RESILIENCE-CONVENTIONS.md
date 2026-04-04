# Resilience Conventions — FrenchExDev.Net

## Packages (in Directory.Packages.props)

- `Microsoft.Extensions.Resilience` — for DI-integrated resilience pipelines
- `Polly.Core` — for standalone resilience strategies
- `Microsoft.Extensions.Http.Resilience` — for HttpClient resilience

## Standard pipeline

```csharp
services.AddResiliencePipeline("default", builder =>
{
    builder
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential,
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromSeconds(15),
        })
        .AddTimeout(TimeSpan.FromSeconds(10));
});
```

## HttpClient

```csharp
services.AddHttpClient("external-api")
    .AddStandardResilienceHandler();
```

## Integration with Result<T>

```csharp
var pipeline = serviceProvider.GetRequiredService<ResiliencePipeline>();
var result = await Result.FromTryAsync(
    () => pipeline.ExecuteAsync(async ct => await httpClient.GetAsync(url, ct)));
```

## Process invocations (BinaryWrapper)

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 2 })
    .AddTimeout(TimeSpan.FromMinutes(5))
    .Build();

await pipeline.ExecuteAsync(async ct => await RunProcessAsync(args, ct));
```

## Named pipelines per project

Each project that needs resilience should define its own named pipeline:
- `"binary-wrapper-scrape"` — for scraping CLI tools (retries, long timeout)
- `"http-external"` — for external HTTP calls (retries, circuit breaker)
- `"file-system"` — for file operations (retries on lock, short timeout)
