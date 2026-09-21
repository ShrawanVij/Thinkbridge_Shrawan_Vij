# Day 18 — Background Jobs

## Objective
Move slow work off the request thread: implement a `BackgroundService` that drains a queue, contrast it with `IHostedService` and Hangfire, and handle graceful shutdown via the cancellation token.

---

## 1. The Queue — `Channel<T>` as a Singleton

`Jobs/EmailQueue.cs` wraps a `System.Threading.Channels.Channel<T>`. It's registered as a **singleton** so the same instance is shared between the producer (an API endpoint) and the consumer (the background worker):

```csharp
public record EmailRequest(string Message);

public class EmailQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

    public ValueTask EnqueueAsync(string message) =>
        _channel.Writer.WriteAsync(message);

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
```

---

## 2. The `BackgroundService` — `EmailWorker`

`Jobs/EmailWorker.cs`:

```csharp
public class EmailWorker : BackgroundService
{
    private readonly EmailQueue _queue;
    private readonly ILogger<EmailWorker> _logger;

    public EmailWorker(EmailQueue queue, ILogger<EmailWorker> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailWorker started");
        try
        {
            await foreach (var message in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await Task.Delay(2000, stoppingToken); // simulated slow work
                    _logger.LogInformation("Processed queued email: {Message}", message);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to process queued email: {Message}", message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected: stoppingToken was cancelled during shutdown
        }
        _logger.LogInformation("EmailWorker stopped");
    }
}
```

**How the shutdown stays clean:** `BackgroundService` implements `IHostedService` for you — the host calls `StartAsync`, which kicks `ExecuteAsync` off as a background `Task`, and on shutdown calls `StopAsync`, which cancels `stoppingToken` and then *waits* for `ExecuteAsync` to actually return (up to `HostOptions.ShutdownTimeout`, 30s by default) before considering shutdown complete.

`_queue.ReadAllAsync(stoppingToken)` awaits `WaitToReadAsync` internally, which observes that same token — so the moment it's cancelled, the `await foreach` throws `OperationCanceledException`, caught by the outer `try/catch` so the worker exits quietly instead of logging an unhandled exception. The `Task.Delay(2000, stoppingToken)` around the in-flight item is also token-aware, so an item being processed *during* shutdown is cancelled rather than left to run indefinitely.

---

## 3. Wiring — `Program.cs`

```csharp
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailWorker>();
```

```csharp
app.MapPost("/jobs/email", async (EmailQueue queue, EmailRequest request) =>
{
    await queue.EnqueueAsync(request.Message);
    return Results.Accepted();
});
```

---

## 4. Verified Locally

Built clean, ran the app, and enqueued a job:

```
curl -X POST http://localhost:5299/jobs/email -H "Content-Type: application/json" -d '{"message":"see you in the logs"}'
→ HTTP_STATUS:202   (returned instantly, before the 2s "slow work" ran)
```

Console output confirmed the worker picked it up asynchronously, ~2 seconds later:

```
[11:37:37 INF] EmailWorker started
[11:37:37 INF] Now listening on: http://localhost:5299
[11:37:37 INF] Application started. Press Ctrl+C to shut down.
[11:37:45 INF] Processed queued email: see you in the logs
```

The request thread was never blocked waiting for the simulated work to finish.

---

## 5. `IHostedService` vs `BackgroundService` vs Hangfire

- **`IHostedService`** — the raw interface (`StartAsync`/`StopAsync`). You own the looping and exception handling yourself.
- **`BackgroundService`** — an abstract base class that implements `IHostedService` for you; you just override `ExecuteAsync(CancellationToken stoppingToken)` and loop. This is what `EmailWorker` uses, and is the right default for in-process work.
- **Hangfire** — a separate package (`Hangfire.Core` + a storage provider) for **persistent, scheduled/recurring, distributed** jobs: survives app restarts, supports cron scheduling and delayed jobs, retries automatically, ships a dashboard (`/hangfire`) for job history and manual retries, and can be worked by multiple app instances against shared storage. A `Channel<T>` queue lives only in one process's memory — it's gone if that process dies.