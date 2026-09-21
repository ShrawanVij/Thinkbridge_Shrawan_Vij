using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace QuotesApi.Tests;

public class EngagementEndpointTests
{
    [Fact]
    public async Task AuditLogs_RequiresAuthentication()
    {
        var factory = TestFactory.CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/engagement/audit-logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuditLogs_AuthenticatedCaller_GetsAPageBackWithoutError()
    {
        // Engagement only ever gets written to asynchronously (via the
        // Service Bus consumer, which isn't running against a live broker in
        // this test host) — this test is about the read endpoint existing
        // and responding correctly, not about a specific log having arrived.
        var factory = TestFactory.CreateFactory<MultiUserTestAuthHandler>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "1");

        var response = await client.GetAsync("/api/engagement/audit-logs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuditLogs_InvalidPageSize_Returns400()
    {
        var factory = TestFactory.CreateFactory<MultiUserTestAuthHandler>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "1");

        var response = await client.GetAsync("/api/engagement/audit-logs?size=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
