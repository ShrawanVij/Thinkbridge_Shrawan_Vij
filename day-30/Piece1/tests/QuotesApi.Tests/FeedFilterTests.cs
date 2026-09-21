using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace QuotesApi.Tests;

public class FeedFilterTests
{
    [Fact]
    public async Task Feed_Search_MatchesAuthorOrText_EvenOutsideTheFirstPage()
    {
        var factory = TestFactory.CreateFactory<MultiUserTestAuthHandler>();
        var client = AsUser(factory, 1);

        // Enough unrelated quotes to push the match past a small page size —
        // this is the exact scenario client-side filtering-of-one-page missed.
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/cqrs/quotes", new { author = $"Filler {i}", text = "Nothing special." });
        }

        await client.PostAsJsonAsync("/cqrs/quotes", new { author = "Ada Lovelace", text = "That brain of mine is something more than merely mortal." });

        var response = await client.GetFromJsonAsync<JsonElement>("/cqrs/quotes/feed?page=1&size=2&search=Lovelace");
        var authors = response.EnumerateArray().Select(q => q.GetProperty("author").GetString()).ToList();

        Assert.Contains("Ada Lovelace", authors);
    }

    [Fact]
    public async Task Feed_Mine_OnlyReturnsCallersOwnQuotes()
    {
        var factory = TestFactory.CreateFactory<MultiUserTestAuthHandler>();

        var user1 = AsUser(factory, 1);
        await user1.PostAsJsonAsync("/cqrs/quotes", new { author = "User 1", text = "User 1's quote." });

        var user2 = AsUser(factory, 2);
        await user2.PostAsJsonAsync("/cqrs/quotes", new { author = "User 2", text = "User 2's quote." });

        var mine = await user1.GetFromJsonAsync<JsonElement>("/cqrs/quotes/feed?mine=true");
        var authors = mine.EnumerateArray().Select(q => q.GetProperty("author").GetString()).ToList();

        Assert.Single(authors);
        Assert.Contains("User 1", authors);
    }

    private static HttpClient AsUser(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, int userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", userId.ToString());
        return client;
    }
}
