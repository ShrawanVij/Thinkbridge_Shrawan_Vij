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
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using QuotesApi.Features.Quotes;
using QuotesApi.Jobs;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

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

if (!string.IsNullOrEmpty(
    builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services
        .AddOpenTelemetry()
        .UseAzureMonitor();
}

builder.Services
    .AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint =
                    new Uri("http://localhost:4317");
            });
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

builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddTransient<IQuoteFormatter, QuoteFormatter>();

builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailWorker>();

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

app.Use(async (ctx, next) =>
{
    using (LogContext.PushProperty("TraceId", ctx.TraceIdentifier))
    {
        await next();
    }
});

app.UseCors("AngularDev");
app.UseAuthentication();
app.UseAuthorization();
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

app.MapPost("/jobs/email", async (EmailQueue queue, EmailRequest request) =>
{
    await queue.EnqueueAsync(request.Message);
    return Results.Accepted();
});

app.MapQuoteEndpoints();
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

app.MapPost("/cqrs/quotes", async (
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

app.MapGet("/cqrs/quotes/feed", async (
    IMediator mediator,
    int? page,
    int? size,
    string? sort) =>
{
    var sortOrder = sort?.ToLowerInvariant() switch
    {
        "oldest" => QuoteSortOrder.OldestFirst,
        "author" => QuoteSortOrder.AuthorAsc,
        _ => QuoteSortOrder.NewestFirst,
    };

    var result = await mediator.Send(
        new GetQuoteFeedQuery(page ?? 1, size, sortOrder));

    return Results.Ok(result);
});

app.MapGet("/cqrs/quotes/feed-dapper", async (
    IMediator mediator,
    int? page,
    int? size) =>
{
    var result = await mediator.Send(
        new GetQuoteFeedDapperQuery(page ?? 1, size ?? 10));

    return Results.Ok(result);
});

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
});

app.Run();

public partial class Program { }