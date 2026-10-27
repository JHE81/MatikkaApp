using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Matikka.Client.Storage;
using Matikka.Core.Progress;
using Matikka.Core.Sync;

namespace Matikka.Client.Tests;

/// <summary>Palvelimen synkronointisäännöt muistissa: riittää asiakkaan logiikan testaamiseen.</summary>
internal sealed class FakeSyncServer : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Json = ClientJsonContext.Default.Options;

    private readonly Dictionary<string, Guid> _accounts = new();
    private readonly List<(long Seq, Guid Account, GameEvent Event)> _events = new();
    private long _seq;

    public bool Offline { get; set; }

    public int AccountCount => _accounts.Count;

    public int EventCount => _events.Count;

    public void RevokeAllTokens() => _accounts.Clear();

    public HttpClient CreateClient() => new(this) { BaseAddress = new Uri("https://test.local/") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (Offline)
            throw new HttpRequestException("Ei verkkoa");

        string path = request.RequestUri!.AbsolutePath;

        if (request.Method == HttpMethod.Post && path == "/api/accounts")
        {
            var token = Guid.NewGuid().ToString("N");
            var id = Guid.NewGuid();
            _accounts[token] = id;
            return JsonResponse(new AccountCreatedResponse { AccountId = id, Token = token });
        }

        if (request.Headers.Authorization?.Parameter is not { } bearer || !_accounts.TryGetValue(bearer, out Guid account))
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);

        if (request.Method == HttpMethod.Delete && path == "/api/accounts/me")
        {
            _accounts.Remove(bearer);
            _events.RemoveAll(e => e.Account == account);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (request.Method == HttpMethod.Post && path == "/api/sync")
        {
            var body = (await request.Content!.ReadFromJsonAsync<SyncRequest>(Json, ct))!;
            var submitted = body.Events.Select(e => e.Id).ToHashSet();

            foreach (var e in body.Events)
            {
                if (!_events.Any(x => x.Account == account && x.Event.Id == e.Id))
                    _events.Add((++_seq, account, e));
            }

            var mine = _events.Where(x => x.Account == account).ToList();
            return JsonResponse(new SyncResponse
            {
                AcceptedIds = submitted.ToList(),
                Incoming = mine.Where(x => x.Seq > body.SinceSeq && !submitted.Contains(x.Event.Id)).Select(x => x.Event).ToList(),
                LatestSeq = mine.Count == 0 ? 0 : mine.Max(x => x.Seq),
            });
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage JsonResponse<T>(T value) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(value, options: Json) };
}
