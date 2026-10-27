using Matikka.Client.Storage;
using Matikka.Core.Progress;

namespace Matikka.Client.Progress;

/// <summary>
/// Offline-first-edistyminen: jokainen pelattu tehtävä tallennetaan heti laitteelle ja
/// edistymismalli päivittyy siitä. Verkkoa ei tarvita pelaamiseen.
/// </summary>
public sealed class ProgressService(IEventStore store, TimeProvider clock)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ProgressModel _model = new();
    private bool _loaded;

    /// <summary>Malli muuttui (oma tapahtuma tai synkronointi toiselta laitteelta).</summary>
    public event Action? Changed;

    /// <summary>Uusi oma tapahtuma tallennettu. Synkronointi kuuntelee tätä.</summary>
    public event Action? EventRecorded;

    public TimeProvider Clock => clock;

    public async Task<ProgressModel> GetModelAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _model;
    }

    public async Task RecordAsync(GameEvent gameEvent, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);

        await _gate.WaitAsync(ct);
        try
        {
            await store.AppendAsync(gameEvent, ct);
            if (!_model.TryAppend(gameEvent))
                _model = ProgressModel.Replay(await store.GetAllAsync(ct));
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke();
        EventRecorded?.Invoke();
    }

    /// <summary>Yhdistää palvelimelta tulleet tapahtumat ja laskee mallin uudelleen.</summary>
    public async Task MergeAsync(IReadOnlyCollection<GameEvent> incoming, CancellationToken ct = default)
    {
        if (incoming.Count == 0)
            return;

        await EnsureLoadedAsync(ct);

        int added;
        await _gate.WaitAsync(ct);
        try
        {
            added = await store.AddFromServerAsync(incoming, ct);
            if (added > 0)
                _model = ProgressModel.Replay(await store.GetAllAsync(ct));
        }
        finally
        {
            _gate.Release();
        }

        if (added > 0)
            Changed?.Invoke();
    }

    /// <summary>Tyhjentää mallin, kun laitteen tiedot on poistettu.</summary>
    internal async Task ResetAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _model = new ProgressModel();
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke();
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loaded)
            return;

        await _gate.WaitAsync(ct);
        try
        {
            if (_loaded)
                return;

            await store.InitializeAsync(ct);
            _model = ProgressModel.Replay(await store.GetAllAsync(ct));
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
