using System.Diagnostics;

namespace QuotesApi.Observability;

// The one place this app names its own spans, so the tracing setup in Program.cs has a single
// source name to subscribe with .AddSource(). Framework/library spans (ASP.NET Core, EF Core,
// the Azure SDK) come from their own sources and don't need this.
public static class AppActivitySource
{
    public const string Name = "QuotesApi";

    public static readonly ActivitySource Instance = new(Name);
}
