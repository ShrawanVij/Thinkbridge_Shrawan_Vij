using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using QuotesApi.Middleware;
using QuotesApi.Modules.Collections;
using QuotesApi.Modules.Collections.Data;
using QuotesApi.Modules.Engagement;
using QuotesApi.Modules.Engagement.Data;
using QuotesApi.Modules.Identity;
using QuotesApi.Modules.Identity.Authorization;
using QuotesApi.Modules.Identity.Data;
using QuotesApi.Modules.Identity.Models;
using QuotesApi.Modules.Quotes;
using QuotesApi.Modules.Quotes.Data;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Input limits, same as the pre-split app: cap the request body Kestrel will
// even buffer, before any per-field validation gets a chance to run.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 64 * 1024;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Brute-force protection on login — unchanged from before the split.
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
        policy.WithOrigins(
                "http://localhost:4200", "http://127.0.0.1:4200", "http://localhost:4210", "http://127.0.0.1:4210",
                "https://thankful-wave-06e439500.7.azurestaticapps.net",
                "https://yellow-field-0c0e7f300.3.azurestaticapps.net")
            .AllowAnyHeader()
            .AllowAnyMethod()
            // Required so the browser stores/sends the HttpOnly refreshToken
            // cookie on cross-origin requests.
            .AllowCredentials());
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddSerilog(config => config
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Each module owns its own DbContext, its own tables, its own hosted
// services — the Host only ever calls AddXModule. It never references a
// module's DbContext type or entities beyond mapping that module's own
// endpoints (below), which is the one place this composition root still
// reaches slightly past "just wiring" — matching how the pre-split app
// already used QuoteDbContext directly for a handful of ops endpoints.
builder.Services.AddQuotesModule(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCollectionsModule(builder.Configuration);
builder.Services.AddEngagementModule(builder.Configuration);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<QuotesApi.Modules.Quotes.Features.CreateQuoteCommand>());

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is not configured.");

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

            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("can-edit-quotes", policy => policy.RequireClaim("scope", "quotes.write"));
    options.AddPolicy("can-edit-collections", policy => policy.RequireClaim("scope", "quotes.write"));

    // Same requirement, two policies — the resource (a quote id) plus the
    // requirement is enough to answer both "can edit" and "can delete"; the
    // action itself is enforced by which endpoint required which policy.
    options.AddPolicy("can-edit-own-quote", policy => policy.AddRequirements(new CanModifyOwnQuoteRequirement()));
    options.AddPolicy("can-delete-own-quote", policy => policy.AddRequirements(new CanModifyOwnQuoteRequirement()));
});

var app = builder.Build();

app.UseForwardedHeaders();

app.UseHsts();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
    ctx.Response.Headers["Cache-Control"] = "no-store";
    await next();
});

app.UseCors("AngularDev");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<ExceptionMiddleware>();

// Each module's own database, created independently — no shared migrations
// history, because no two modules share a table.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<QuotesDbContext>().Database.EnsureCreated();
    scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreated();
    scope.ServiceProvider.GetRequiredService<CollectionsDbContext>().Database.EnsureCreated();
    scope.ServiceProvider.GetRequiredService<EngagementDbContext>().Database.EnsureCreated();
}

// Development test user — unchanged behavior, now seeded into Identity's own database.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

    if (!db.Users.Any(u => u.Email == "test@example.com"))
    {
        db.Users.Add(new User
        {
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!")
        });

        db.SaveChanges();
    }
}

app.MapGet("/", () => "Quotes API is running!");

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapQuotesEndpoints();
app.MapTagsEndpoints();
app.MapAuthEndpoints();
app.MapCollectionsEndpoints();
app.MapEngagementEndpoints();

app.Run();

public partial class Program { }
