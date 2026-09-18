using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;

namespace QuotesApi.Tests;

public class CollectionsEndpointTests
{
    [Fact]
    public async Task CreateCollection_OwnerIsTheAuthenticatedCaller_NotWhateverTheBodySays()
    {
        var client = CreateClientAsUser(TestFactory.CreateFactory<MultiUserTestAuthHandler>(), 1);

        // No OwnerId field exists on the request anymore — this is the
        // regression test for the bug where the client used to be able to
        // hand in someone else's user id and "own" a collection for them.
        var response = await client.PostAsJsonAsync("/collections", new { name = "My Favorites" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("ownerId").GetInt32());
    }

    [Fact]
    public async Task ListMyCollections_OnlyReturnsCallersOwn_NotOtherUsers()
    {
        var factory = TestFactory.CreateFactory<MultiUserTestAuthHandler>();

        var user1 = CreateClientAsUser(factory, 1);
        await user1.PostAsJsonAsync("/collections", new { name = "User 1's collection" });

        var user2 = CreateClientAsUser(factory, 2);
        await user2.PostAsJsonAsync("/collections", new { name = "User 2's collection" });

        var mineResponse = await user1.GetAsync("/collections");
        Assert.Equal(HttpStatusCode.OK, mineResponse.StatusCode);

        var mine = await mineResponse.Content.ReadFromJsonAsync<JsonElement>();
        var names = mine.EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();

        Assert.Single(names);
        Assert.Contains("User 1's collection", names);
    }

    [Fact]
    public async Task GetCollection_OwnedByAnotherUser_Returns403()
    {
        var factory = TestFactory.CreateFactory<MultiUserTestAuthHandler>();

        var owner = CreateClientAsUser(factory, 1);
        var created = await owner.PostAsJsonAsync("/collections", new { name = "User 1's collection" });
        var collectionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var otherUser = CreateClientAsUser(factory, 2);
        var response = await otherUser.GetAsync($"/collections/{collectionId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddItem_ToAnotherUsersCollection_Returns403()
    {
        var factory = TestFactory.CreateFactory<MultiUserTestAuthHandler>();

        var owner = CreateClientAsUser(factory, 1);
        var created = await owner.PostAsJsonAsync("/collections", new { name = "User 1's collection" });
        var collectionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var otherUser = CreateClientAsUser(factory, 2);
        var response = await otherUser.PostAsJsonAsync($"/collections/{collectionId}/items", new { quoteId = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpClient CreateClientAsUser(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, int userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", userId.ToString());
        return client;
    }
}
