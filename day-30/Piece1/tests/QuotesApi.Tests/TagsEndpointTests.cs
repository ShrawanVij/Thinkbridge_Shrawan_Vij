using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace QuotesApi.Tests;

public class TagsEndpointTests
{
    [Fact]
    public async Task AttachTag_ThenGetQuote_ReturnsIt()
    {
        var factory = TestFactory.CreateFactory<MultiUserTestAuthHandler>();
        var client = AsUser(factory, 1);

        var created = await client.PostAsJsonAsync("/cqrs/quotes", new { author = "Rumi", text = "The wound is the place where the light enters you." });
        var quoteId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var tagResponse = await client.PostAsJsonAsync($"/api/quotes/{quoteId}/tags", new { name = "wisdom" });
        Assert.Equal(HttpStatusCode.OK, tagResponse.StatusCode);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/quotes/{quoteId}");
        var tagNames = detail.GetProperty("tags").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();

        Assert.Contains("wisdom", tagNames);
    }

    [Fact]
    public async Task AttachSameTagTwice_IsIdempotent()
    {
        var factory = TestFactory.CreateFactory<MultiUserTestAuthHandler>();
        var client = AsUser(factory, 1);

        var created = await client.PostAsJsonAsync("/cqrs/quotes", new { author = "Rumi", text = "Quote text here." });
        var quoteId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        await client.PostAsJsonAsync($"/api/quotes/{quoteId}/tags", new { name = "wisdom" });
        var second = await client.PostAsJsonAsync($"/api/quotes/{quoteId}/tags", new { name = "wisdom" });

        var tags = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(tags.EnumerateArray());
    }

    [Fact]
    public async Task AttachTag_ToAnotherUsersQuote_Returns403()
    {
        var factory = TestFactory.CreateFactory<MultiUserTestAuthHandler>();
        var owner = AsUser(factory, 1);
        var created = await owner.PostAsJsonAsync("/cqrs/quotes", new { author = "Rumi", text = "Quote text here." });
        var quoteId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var otherUser = AsUser(factory, 2);
        var response = await otherUser.PostAsJsonAsync($"/api/quotes/{quoteId}/tags", new { name = "hijacked" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DetachTag_RemovesIt()
    {
        var factory = TestFactory.CreateFactory<MultiUserTestAuthHandler>();
        var client = AsUser(factory, 1);

        var created = await client.PostAsJsonAsync("/cqrs/quotes", new { author = "Rumi", text = "Quote text here." });
        var quoteId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var attachResponse = await client.PostAsJsonAsync($"/api/quotes/{quoteId}/tags", new { name = "wisdom" });
        var tagId = (await attachResponse.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetInt32();

        var detachResponse = await client.DeleteAsync($"/api/quotes/{quoteId}/tags/{tagId}");
        Assert.Equal(HttpStatusCode.NoContent, detachResponse.StatusCode);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/quotes/{quoteId}");
        Assert.Empty(detail.GetProperty("tags").EnumerateArray());
    }

    private static HttpClient AsUser(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, int userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", userId.ToString());
        return client;
    }
}
