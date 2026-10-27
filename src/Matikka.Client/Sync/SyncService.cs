using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Matikka.Client.Profile;
using Matikka.Client.Progress;
using Matikka.Client.Storage;
using Matikka.Core.Sync;

namespace Matikka.Client.Sync;

public enum SyncState
{
    /// <summary>Palvelinta ei ole määritetty: sovellus toimii pelkästään paikallisesti.</summary>
    Disabled,
    Idle,
    Syncing,
    Synced,

    /// <summary>Ei verkkoa. Tapahtumat odottavat laitteella ja lähtevät seuraavalla kerralla.</summary>
    Offline,
    Error,
}

/// <summary>
/// Lähettää laitteella odottavat tapahtumat palvelimelle ja hakee muiden laitteiden tapahtumat.
/// Pelaaminen ei koskaan odota tätä: synkronointi käynnistyy taustalla tallennuksen jälkeen.
///
/// Tilin luonti on nimetön: palvelin antaa satunnaisen tunnisteen, joka tallennetaan
/// laitteen suojattuun tallennustilaan. Myöhemmin tähän voi liittää Sign in with Apple-
/// / Google-kirjautumisen ja sovelluskaupan tilauksen, jolloin edistyminen siirtyy laitteelta toiselle.
/// </summary>
public sealed class SyncService : IDisposable
{
    internal const string TokenKey = "account-token";
    internal const string CursorKey = "sync-seq";
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);

    private readonly HttpClient? _http;
    private readonly IEventStore _store;
    private readonly ISecretStore _secrets;
    private readonly ProgressService _progress;
    private readonly ProfileService _profile;
    private readonly object _gate = new();
    private Task? _running;
    private CancellationTokenSource? _debounce;
    private bool _started;

    /// <param name="http">Null, jos palvelinta ei käytetä (pelkkä paikallinen tila).</param>
    public SyncService(HttpClient? http, IEventStore store, ISecretStore secrets, ProgressService progress, ProfileService profile)
    {
        _http = http;
        _store = store;
        _secrets = secrets;
        _progress = progress;
        _profile = profile;
        State = http?.BaseAddress == null ? SyncState.Disabled : SyncState.Idle;
    }

    public SyncState State { get; private set; }

    public DateTimeOffset? LastSyncedAt { get; private set; }

    public event Action? StateChanged;

    /// <summary>Kytkee automaattisen synkronoinnin: käynnistyksessä ja aina uuden tuloksen jälkeen.</summary>
    public void Start()
    {
        if (_started || State == SyncState.Disabled)
            return;

        _started = true;
        _progress.EventRecorded += ScheduleSync;
        _ = SyncAsync();
    }

    /// <summary>Synkronoi nyt. Jos synkronointi on jo käynnissä, palauttaa saman tehtävän.</summary>
    public Task SyncAsync(CancellationToken ct = default)
    {
        if (State == SyncState.Disabled)
            return Task.CompletedTask;

        lock (_gate)
        {
            if (_running is { IsCompleted: false })
                return _running;

            _running = RunAsync(ct);
            return _running;
        }
    }

    /// <summary>
    /// Poistaa tilin palvelimelta ja kaikki tiedot laitteelta. Sovelluskaupat (Apple) vaativat,
    /// että tilin voi poistaa sovelluksen sisältä.
    /// </summary>
    public async Task DeleteAllDataAsync(CancellationToken ct = default)
    {
        string? token = await _secrets.GetAsync(TokenKey);
        if (_http?.BaseAddress != null && token != null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, "api/accounts/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage response = await _http.SendAsync(request, ct);

            // 401: tili on jo poistettu palvelimelta. Muu virhe keskeyttää, jotta käyttäjä voi yrittää uudelleen.
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                response.EnsureSuccessStatusCode();
        }

        await _secrets.RemoveAsync(TokenKey);
        await _store.DeleteAllAsync(ct);
        await _progress.ResetAsync();
        _profile.Reset();
        LastSyncedAt = null;
        SetState(_http?.BaseAddress == null ? SyncState.Disabled : SyncState.Idle);
    }

    public void Dispose()
    {
        _progress.EventRecorded -= ScheduleSync;
        _debounce?.Cancel();
        _debounce?.Dispose();
    }

    private void ScheduleSync()
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            _debounce?.Cancel();
            _debounce?.Dispose();
            _debounce = cts = new CancellationTokenSource();
        }

        _ = Task.Delay(Debounce, cts.Token).ContinueWith(
            t => { if (!t.IsCanceled) _ = SyncAsync(); },
            TaskScheduler.Default);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        SetState(SyncState.Syncing);
        try
        {
            bool retriedAfterUnauthorized = false;
            for (int round = 0; round < 50; round++) // varmuusraja: loput seuraavalla kerralla
            {
                SyncOutcome outcome = await SyncBatchAsync(ct);
                if (outcome == SyncOutcome.Done)
                    break;

                if (outcome == SyncOutcome.Unauthorized)
                {
                    // Tili on poistunut palvelimelta: luodaan uusi ja lähetetään kaikki uudestaan.
                    if (retriedAfterUnauthorized)
                        throw new InvalidOperationException("Palvelin hylkäsi myös uuden tilin.");

                    retriedAfterUnauthorized = true;
                    await _secrets.RemoveAsync(TokenKey);
                    await _store.RemoveValueAsync(CursorKey, ct);
                    await _store.MarkAllUnsyncedAsync(ct);
                }
            }

            LastSyncedAt = _progress.Clock.GetLocalNow();
            SetState(SyncState.Synced);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            SetState(SyncState.Offline);
        }
        catch (Exception)
        {
            SetState(SyncState.Error);
        }
    }

    private enum SyncOutcome
    {
        Done,
        /// <summary>Lisää lähetettävää tai haettavaa: uusi kierros heti.</summary>
        MoreToSend,
        Unauthorized,
    }

    private async Task<SyncOutcome> SyncBatchAsync(CancellationToken ct)
    {
        string token = await EnsureTokenAsync(ct);
        string? cursorText = await _store.GetValueAsync(CursorKey, ct);
        long cursor = long.TryParse(cursorText, out long parsed) ? parsed : 0;

        var pending = await _store.GetUnsyncedAsync(SyncRequest.MaxEventsPerRequest, ct);
        var body = new SyncRequest { SinceSeq = cursor, Events = pending.ToList() };

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/sync")
        {
            Content = JsonContent.Create(body, ClientJsonContext.Default.SyncRequest),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await _http!.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return SyncOutcome.Unauthorized;
        response.EnsureSuccessStatusCode();

        SyncResponse result = await response.Content.ReadFromJsonAsync(ClientJsonContext.Default.SyncResponse, ct)
            ?? throw new InvalidOperationException("Tyhjä vastaus palvelimelta.");

        await _store.MarkSyncedAsync(result.AcceptedIds, ct);
        await _progress.MergeAsync(result.Incoming, ct);
        await _store.SetValueAsync(CursorKey, result.LatestSeq.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);

        return pending.Count == SyncRequest.MaxEventsPerRequest || result.HasMore
            ? SyncOutcome.MoreToSend
            : SyncOutcome.Done;
    }

    private async Task<string> EnsureTokenAsync(CancellationToken ct)
    {
        string? token = await _secrets.GetAsync(TokenKey);
        if (!string.IsNullOrEmpty(token))
            return token;

        using HttpResponseMessage response = await _http!.PostAsync("api/accounts", content: null, ct);
        response.EnsureSuccessStatusCode();

        AccountCreatedResponse created = await response.Content.ReadFromJsonAsync(ClientJsonContext.Default.AccountCreatedResponse, ct)
            ?? throw new InvalidOperationException("Tyhjä vastaus palvelimelta.");

        await _secrets.SetAsync(TokenKey, created.Token);
        return created.Token;
    }

    private void SetState(SyncState state)
    {
        State = state;
        StateChanged?.Invoke();
    }
}
