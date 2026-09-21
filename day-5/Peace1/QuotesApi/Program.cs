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

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddTransient<IQuoteFormatter, QuoteFormatter>();

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

app.MapQuoteEndpoints();
app.MapAuthEndpoints();
app.MapCollectionEndpoints();

app.Run();

public partial class Program { }