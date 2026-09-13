using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Extensions;
using QuotesApi.Middleware;
using QuotesApi.Repositories;
using QuotesApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using QuotesApi.Models;
using QuotesApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using Serilog.Context;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using QuotesApi.Observability;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using QuotesApi.Features.Quotes;
using QuotesApi.Jobs;
using QuotesApi.Messaging;
using QuotesApi.Outbox;
using QuotesApi.Caching;
using QuotesApi.Resilience;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Azure.Messaging.ServiceBus;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using MediatR;

// Older Azure SDK diagnostics defaulted to a legacy DiagnosticListener-based scope instead of
// System.Diagnostics.ActivitySource; this opts every Azure client (ServiceBusClient included)
// into ActivitySource-based spans, which is what .AddSource("Azure.*") below actually listens
// to. Without this switch, Send/Process spans — and the W3C trace-context propagation that
// rides along with them — never happen at all, silently.
AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

var builder = WebApplication.CreateBuilder(args);

// Input limits: cap the request body Kestrel will even buffer, before any model binding or
// per-field validation (Author <= 100 chars, Text <= 1000 chars, etc.) gets a chance to reject
// an oversized payload. 64KB is generous for anything this API accepts as JSON.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 64 * 1024;
});

// Defensive correctness, not a fix for a confirmed bug: Azure Container Apps' ingress happens to
// surface the real client IP straight to Kestrel already (verified live — Connection.RemoteIpAddress
// matched the true caller), via its own x-envoy-external-address header rather than the standard
// X-Forwarded-For. This is still worth having in case that ever changes, or if this app ends up
// behind a different reverse proxy that does use X-Forwarded-For without it. KnownNetworks/
// KnownProxies are cleared because there's no fixed, known proxy IP to pin to here — Container
// Apps' ingress is the only thing that can reach Kestrel directly, so trusting it is reasonable.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Rate limiting: /api/auth/login had no brute-force protection at all — a caller could try
// passwords as fast as the network allowed. Partitioned by remote IP so one attacker doesn't
// exhaust the budget of every other IP calling the same endpoint.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 5,
                QueueLimit = 0
            }));
});

// API versioning: header-based (not URL-segment) so existing callers that never send the header
// keep working unchanged against v1 — versioning the surface without breaking it.
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new HeaderApiVersionReader("X-Api-Version");
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
        policy.WithOrigins(
                "http://localhost:4200", "http://127.0.0.1:4200", "http://localhost:4210", "http://127.0.0.1:4210",
                "https://thankful-wave-06e439500.7.azurestaticapps.net")
            .AllowAnyHeader()
            .AllowAnyMethod()
            // Required so the browser will store/send the HttpOnly refreshToken
            // cookie on cross-origin requests (frontend :4200, backend :5220).
            .AllowCredentials());
});

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("Jwt"));

var otel = builder.Services.AddOpenTelemetry();

if (!string.IsNullOrEmpty(
    builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    // Sets the Azure Monitor exporter and its own default instrumentation. The .WithTracing()
    // call below configures the same provider further — AddOpenTelemetry() is idempotent, so
    // this doesn't create a second, competing pipeline.
    otel.UseAzureMonitor();
}

otel.WithTracing(tracing =>
{
    tracing
        .AddSource(AppActivitySource.Name) // this app's own spans (outbox relay -> publish)
        .AddSource("Azure.*") // Service Bus send/process spans
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddHttpClientInstrumentation();

    if (builder.Environment.IsDevelopment())
    {
        // Prints every span to the console — enough to confirm a trace id is shared across
        // the API request, the outbox relay, and the Service Bus worker without needing a
        // live App Insights resource to look at.
        tracing.AddConsoleExporter();
    }

    var otlpEndpoint = builder.Configuration["Otel:OtlpEndpoint"];
    if (!string.IsNullOrEmpty(otlpEndpoint))
    {
        tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
    }
});

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddInfrastructure();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

builder.Services
    .AddHttpClient("resilient-service", client =>
    {
        client.BaseAddress = new Uri("https://httpbin.org/");
    })
    .AddResilienceHandler("default", resilience =>
    {
        resilience.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromSeconds(1)
        });

        resilience.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 2,
            BreakDuration = TimeSpan.FromSeconds(10)
        });

        resilience.AddTimeout(TimeSpan.FromSeconds(10));
    });

builder.Services.AddSingleton<FlakyDependencyState>();

builder.Services
    .AddHttpClient("flaky-dependency", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Resilience:FlakyDependencyBaseUrl"]!);
    })
    .AddResilienceHandler("flaky-dependency-pipeline", (resilience, context) =>
    {
        var logger = context.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Resilience.FlakyDependency");

        // Order: bulkhead (admission control, outermost) -> retry (idempotent
        // only) -> circuit breaker -> timeout (per attempt, innermost).
        // Bulkhead decides whether a call is even attempted before anything
        // else spends time on it; timeout bounds each individual attempt
        // the retry/breaker layers make.
        resilience.AddConcurrencyLimiter(permitLimit: 2, queueLimit: 2);

        resilience.AddRetry(new HttpRetryStrategyOptions
        {
            ShouldHandle = args =>
            {
                var isIdempotentMethod = args.Outcome.Result?.RequestMessage?.Method is HttpMethod method &&
                    (method == HttpMethod.Get || method == HttpMethod.Head ||
                     method == HttpMethod.Put || method == HttpMethod.Delete ||
                     method == HttpMethod.Options);

                return ValueTask.FromResult(isIdempotentMethod && HttpClientResiliencePredicates.IsTransient(args.Outcome));
            },
            MaxRetryAttempts = 4,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(200),
            OnRetry = args =>
            {
                logger.LogWarning(
                    "[retry] Attempt {Attempt} after {Delay}ms for {Method} {Uri}",
                    args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds,
                    args.Outcome.Result?.RequestMessage?.Method, args.Outcome.Result?.RequestMessage?.RequestUri);
                return default;
            }
        });

        resilience.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(10),
            MinimumThroughput = 4,
            BreakDuration = TimeSpan.FromSeconds(5),
            OnOpened = args =>
            {
                logger.LogError("[circuit-breaker] OPENED — failing fast for {Duration}s", args.BreakDuration.TotalSeconds);
                return default;
            },
            OnHalfOpened = args =>
            {
                logger.LogWarning("[circuit-breaker] HALF-OPEN — probing with the next call");
                return default;
            },
            OnClosed = args =>
            {
                logger.LogInformation("[circuit-breaker] CLOSED — recovered");
                return default;
            }
        });

        resilience.AddTimeout(TimeSpan.FromSeconds(2));
    });

builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddTransient<IQuoteFormatter, QuoteFormatter>();

builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailWorker>();

builder.Services.Configure<ServiceBusOptions>(
    builder.Configuration.GetSection("ServiceBus"));
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
    return new ServiceBusClient(options.ConnectionString);
});
builder.Services.AddSingleton<QuoteEventPublisher>();
builder.Services.AddHostedService<NotifySubscriptionWorker>();
builder.Services.AddHostedService<AuditSubscriptionWorker>();

builder.Services.AddSingleton<OutboxCrashSimulator>();
builder.Services.AddHostedService<OutboxRelay>();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
});
builder.Services.AddHybridCache();
builder.Services.AddSingleton<DbHitCounter>();

var jwtOptions = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "JWT configuration is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.Key))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("can-edit-quotes", policy =>
    {
        policy.RequireClaim("scope", "quotes.write");
    });
    options.AddPolicy("can-edit-collections", policy =>
    {
        policy.RequireClaim("scope", "quotes.write");
    });
    options.AddPolicy("can-delete-own-quote", policy =>
    {
        policy.AddRequirements(
            new CanDeleteOwnQuoteRequirement());
    });
});

builder.Services.AddScoped<IAuthorizationHandler,
    CanDeleteOwnQuoteHandler>();

var app = builder.Build();

// Must run before anything that reads Connection.RemoteIpAddress (the rate limiter included) —
// it rewrites that property from X-Forwarded-For.
app.UseForwardedHeaders();

app.Use(async (ctx, next) =>
{
    // The W3C trace id (Activity.Current), not HttpContext.TraceIdentifier — the latter is a
    // per-request id local to this process and doesn't match the trace id App Insights uses,
    // so logs couldn't actually be correlated to a trace by it despite the property name.
    var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? ctx.TraceIdentifier;

    using (LogContext.PushProperty("TraceId", traceId))
    {
        await next();
    }
});

// Security headers — the 5 warnings a real ZAP baseline scan found against this app (see
// EXERCISE.md §4). None of these are exploits on their own; they're the kind of defense-in-depth
// a scanner flags because their absence removes a layer that would otherwise help.
app.UseHsts();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
    // This API returns JSON, never HTML/assets meant to be cached — nothing here should be
    // stored by a shared cache or browser disk cache.
    ctx.Response.Headers["Cache-Control"] = "no-store";
    await next();
});

app.UseCors("AngularDev");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<ExceptionMiddleware>();

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<QuoteDbContext>();

    db.Database.Migrate();
}

// Development test user
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<QuoteDbContext>();

    if (!db.Users.Any(u => u.Email == "test@example.com"))
    {
        db.Users.Add(new User
        {
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                "Test123!")
        });

        db.SaveChanges();
    }
}

app.MapGet("/", () => "Quotes API is running!");

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy"
}));

// The endpoints below (through the chaos-test group further down) had no auth at all — anyone
// could enqueue arbitrary email jobs, crash the outbox relay on demand, read every outbox
// message's payload, force a republish of any quote's event, inject a poison message into the
// real topic, or read dead-lettered message bodies. Requiring authentication doesn't change who
// legitimately calls these (nothing public was ever meant to), it just stops an anonymous caller
// on the internet from doing any of it.
app.MapPost("/jobs/email", async (EmailQueue queue, EmailRequest request) =>
{
    await queue.EnqueueAsync(request.Message);
    return Results.Accepted();
})
.RequireAuthorization();

app.MapPost("/outbox/relay/crash-once", (OutboxCrashSimulator crashSimulator) =>
{
    crashSimulator.ArmCrashAfterNextPublish();
    return Results.Accepted();
})
.RequireAuthorization();

app.MapGet("/outbox/messages", async (QuoteDbContext db, CancellationToken ct) =>
{
    var messages = await db.OutboxMessages
        .OrderBy(m => m.OccurredAt)
        .Select(m => new { m.Id, m.Type, m.OccurredAt, m.SentAt })
        .ToListAsync(ct);

    return Results.Ok(messages);
})
.RequireAuthorization();

app.MapPost("/events/quotes/{id:int}/republish", async (int id, QuoteDbContext db, QuoteEventPublisher publisher, CancellationToken ct) =>
{
    var quote = await db.Quotes.FindAsync([id], ct);
    if (quote is null)
    {
        return Results.NotFound();
    }

    // Same MessageId as the original publish ("quote-created-{id}") — this
    // simulates a duplicate delivery, which is what the idempotency check
    // in NotifySubscriptionWorker/AuditSubscriptionWorker is there to catch.
    await publisher.PublishQuoteCreatedAsync(
        new QuoteCreatedEvent(quote.Id, quote.Author, quote.Text, quote.CreatedAt), ct);

    return Results.Accepted();
})
.RequireAuthorization("can-edit-quotes");

app.MapPost("/events/quotes/poison-test", async (QuoteEventPublisher publisher, CancellationToken ct) =>
{
    await publisher.PublishPoisonMessageAsync(ct);
    return Results.Accepted();
})
.RequireAuthorization();

app.MapGet("/events/notify-sub/dead-letters", async (ServiceBusClient client, IOptions<ServiceBusOptions> options, CancellationToken ct) =>
{
    await using var receiver = client.CreateReceiver(
        options.Value.TopicName,
        options.Value.NotifySubscriptionName,
        new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

    var messages = await receiver.PeekMessagesAsync(maxMessages: 20, cancellationToken: ct);

    return Results.Ok(messages.Select(m => new
    {
        m.MessageId,
        Body = m.Body.ToString(),
        m.DeliveryCount,
        m.DeadLetterReason,
        m.DeadLetterErrorDescription
    }));
})
.RequireAuthorization();

// Versioned as v1. Header-based (X-Api-Version), defaulted, so this doesn't change any existing
// route or break a caller that never sends the header — it just makes the version explicit and
// reported (api-supported-versions response header) for the ones that do.
var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1))
    .ReportApiVersions()
    .Build();

app.MapGroup("")
    .WithApiVersionSet(apiVersionSet)
    .MapToApiVersion(1)
    .MapQuoteEndpoints();

app.MapAuthEndpoints();
app.MapCollectionEndpoints();

app.MapGet("/reports/authors-quotes-n1", async (QuoteDbContext db) =>
{
    var result = await db.Quotes
        .GroupBy(q => q.Author)
        .Select(g => new { author = g.Key, quoteCount = g.Count() })
        .ToListAsync();

    return Results.Ok(result);
});

var cqrsQuotes = app.MapGroup("/cqrs/quotes")
    .WithApiVersionSet(apiVersionSet)
    .MapToApiVersion(1);

cqrsQuotes.MapPost("", async (
    CreateQuoteRequest request,
    ClaimsPrincipal user,
    IMediator mediator) =>
{
    var userIdClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    if (!int.TryParse(userIdClaim, out var userId))
    {
        return Results.Unauthorized();
    }

    try
    {
        var result = await mediator.Send(
            new CreateQuoteCommand(request.Author, request.Text, userId));

        return Results.Created($"/cqrs/quotes/{result.Id}", result);
    }
    catch (QuoteValidationException ex)
    {
        return Results.ValidationProblem(ex.Errors);
    }
})
.RequireAuthorization("can-edit-quotes");

const int MaxFeedPageSize = 100;

cqrsQuotes.MapGet("/feed", async (
    IMediator mediator,
    int? page,
    int? size,
    string? sort) =>
{
    var currentPage = page ?? 1;
    var currentSize = size ?? 20;

    if (currentPage < 1 || currentSize < 1 || currentSize > MaxFeedPageSize)
    {
        return Results.BadRequest(
            $"Page must be >= 1 and size must be between 1 and {MaxFeedPageSize}.");
    }

    var sortOrder = sort?.ToLowerInvariant() switch
    {
        "oldest" => QuoteSortOrder.OldestFirst,
        "author" => QuoteSortOrder.AuthorAsc,
        _ => QuoteSortOrder.NewestFirst,
    };

    var result = await mediator.Send(
        new GetQuoteFeedQuery(currentPage, currentSize, sortOrder));

    return Results.Ok(result);
});

cqrsQuotes.MapGet("/feed-dapper", async (
    IMediator mediator,
    int? page,
    int? size) =>
{
    var currentPage = page ?? 1;
    var currentSize = size ?? 20;

    if (currentPage < 1 || currentSize < 1 || currentSize > MaxFeedPageSize)
    {
        return Results.BadRequest(
            $"Page must be >= 1 and size must be between 1 and {MaxFeedPageSize}.");
    }

    var result = await mediator.Send(
        new GetQuoteFeedDapperQuery(currentPage, currentSize));

    return Results.Ok(result);
});

// Chaos-testing endpoints (Days 21-22's resilience demos). /test/flaky/fail in particular flips
// a process-wide flag that makes every caller's request fail — reachable by anyone, that's a
// denial-of-service switch handed to the internet. All of these require auth now.
app.MapGet("/api/flaky-dependency", async (FlakyDependencyState state) =>
{
    await Task.Delay(300); // simulate real-world latency
    return state.ForceFailure ? Results.StatusCode(503) : Results.Ok(new { status = "ok" });
})
.RequireAuthorization();

app.MapPost("/api/flaky-dependency", async (FlakyDependencyState state) =>
{
    await Task.Delay(300);
    return state.ForceFailure ? Results.StatusCode(503) : Results.Ok(new { status = "ok, mutation applied" });
})
.RequireAuthorization();

app.MapPost("/test/flaky/fail", (FlakyDependencyState state) =>
{
    state.Fail();
    return Results.NoContent();
})
.RequireAuthorization();

app.MapPost("/test/flaky/recover", (FlakyDependencyState state) =>
{
    state.Recover();
    return Results.NoContent();
})
.RequireAuthorization();

app.MapGet("/api/flaky-dependency/call", async (IHttpClientFactory httpClientFactory, ILogger<Program> logger) =>
{
    var client = httpClientFactory.CreateClient("flaky-dependency");
    try
    {
        var response = await client.GetAsync("/api/flaky-dependency");
        return Results.Ok(new { statusCode = (int)response.StatusCode });
    }
    catch (BrokenCircuitException)
    {
        logger.LogError("[caller] Circuit is open — failed fast, no call attempted");
        return Results.StatusCode(503);
    }
    catch (RateLimiterRejectedException)
    {
        logger.LogError("[caller] Bulkhead full — request rejected before any attempt");
        return Results.StatusCode(429);
    }
})
.RequireAuthorization();

app.MapPost("/api/flaky-dependency/call", async (IHttpClientFactory httpClientFactory, ILogger<Program> logger) =>
{
    var client = httpClientFactory.CreateClient("flaky-dependency");
    try
    {
        var response = await client.PostAsync("/api/flaky-dependency", null);
        return Results.Ok(new { statusCode = (int)response.StatusCode });
    }
    catch (BrokenCircuitException)
    {
        logger.LogError("[caller] Circuit is open — failed fast, no call attempted");
        return Results.StatusCode(503);
    }
    catch (RateLimiterRejectedException)
    {
        logger.LogError("[caller] Bulkhead full — request rejected before any attempt");
        return Results.StatusCode(429);
    }
})
.RequireAuthorization();

app.MapGet("/api/resilience-test", async (
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger) =>
{
    var client = httpClientFactory.CreateClient("resilient-service");

    logger.LogInformation("Starting resilience test");

    try
    {
        await client.GetAsync("status/503");

        return Results.Ok(new
        {
            message = "Request unexpectedly succeeded"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Resilience test failed after retries");

        return Results.Problem(
            "Request failed after resilience policies were exhausted.");
    }
})
.RequireAuthorization();

app.Run();

public partial class Program { }