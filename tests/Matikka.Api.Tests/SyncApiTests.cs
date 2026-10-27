using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Matikka.Api;
using Matikka.Core.Progress;
using Matikka.Core.Sync;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Matikka.Api.Tests;

/// <summary>Koko API muistivarastolla (ei yhteysmerkkijonoa → InMemorySyncStore).</summary>
public class SyncApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static GameEvent NewEvent(int stars = 3) => new()
    {
        Id = Guid.NewGuid(),
        GameId = "yhtalovaaka",
        SkillId = "algebra.yhtalot",
        Level = 2,
        Seed = 42,
        Outcome = GameOutcome.Solved,
        Moves = 1,
        Par = 1,
        Stars = stars,
        DurationMs = 8000,
        OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-1),
    };

    private async Task<HttpClient> AuthorizedClientAsync()
    {
        HttpClient client = factory.CreateClient();
        var account = await (await client.PostAsync("/api/accounts", null)).Content
            .ReadFromJsonAsync<AccountCreatedResponse>(ApiJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account!.Token);
        return client;
    }

    private static async Task<SyncResponse> SyncAsync(HttpClient client, long since, params GameEvent[] events)
    {
        var response = await client.PostAsJsonAsync("/api/sync", new SyncRequest { SinceSeq = since, Events = events.ToList() }, ApiJson.Options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SyncResponse>(ApiJson.Options))!;
    }

    [Fact]
    public async Task Sync_requires_token()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/sync", new SyncRequest(), ApiJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Events_reach_another_device_once()
    {
        var phone = await AuthorizedClientAsync();
        var e = NewEvent();

        var first = await SyncAsync(phone, 0, e);
        var again = await SyncAsync(phone, 0, e); // sama tapahtuma uudelleen: ei tuplaannu

        Assert.Contains(e.Id, first.AcceptedIds);
        Assert.Empty(again.Incoming);
        Assert.Equal(first.LatestSeq, again.LatestSeq);

        var otherDevice = await SyncAsync(phone, 0);
        Assert.Single(otherDevice.Incoming);
        Assert.Equal(e.Id, otherDevice.Incoming[0].Id);
    }

    [Fact]
    public async Task Accounts_do_not_see_each_other()
    {
        var a = await AuthorizedClientAsync();
        var b = await AuthorizedClientAsync();
        await SyncAsync(a, 0, NewEvent());

        Assert.Empty((await SyncAsync(b, 0)).Incoming);
    }

    [Fact]
    public async Task Invalid_event_is_rejected()
    {
        var client = await AuthorizedClientAsync();

        var response = await client.PostAsJsonAsync("/api/sync",
            new SyncRequest { Events = { NewEvent(stars: 9) } }, ApiJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deleted_account_cannot_sync()
    {
        var client = await AuthorizedClientAsync();
        await SyncAsync(client, 0, NewEvent());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/accounts/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/sync", new SyncRequest(), ApiJson.Options)).StatusCode);
    }
}
