using QuotesHub.Modules.Quotes.Infrastructure;
using QuotesHub.Modules.Quotes.Application;
using QuotesHub.Modules.Collections.Infrastructure;
using QuotesHub.Modules.Identity.Infrastructure;
using QuotesHub.Modules.Engagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// One deployable, four independently-composed modules. Each AddXModule
// call is the only place the Host touches that module's internals — it
// never references a module's Domain or Application projects directly.
builder.Services.AddQuotesModule(builder.Configuration);
builder.Services.AddCollectionsModule(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddEngagementModule(builder.Configuration);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CreateQuoteCommand>());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/", () => "QuotesHub — modular monolith kickoff scaffold");

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/api/quotes", async (CreateQuoteCommand command, MediatR.IMediator mediator) =>
{
    var result = await mediator.Send(command);
    return Results.Created($"/api/quotes/{result.Id}", result);
});

app.Run();
