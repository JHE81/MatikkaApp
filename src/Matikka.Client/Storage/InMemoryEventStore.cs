using Matikka.Core.Progress;

namespace Matikka.Client.Storage;

/// <summary>Muistinvarainen toteutus testeihin ja nopeisiin kokeiluihin. Ei säily uudelleenkäynnistyksen yli.</summary>
public sealed class InMemoryEventStore : IEventStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, (GameEvent Event, bool Synced)> _events = new();
    private readonly Dictionary<string, string> _values = new();

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task AppendAsync(GameEvent gameEvent, CancellationToken ct = default)
    {
        lock (_gate)
            _events.TryAdd(gameEvent.Id, (gameEvent, false));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GameEvent>> GetAllAsync(CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<GameEvent>>(ProgressModel.Order(_events.Values.Select(v => v.Event)).ToList());
    }

    public Task<IReadOnlyList<GameEvent>> GetUnsyncedAsync(int max, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var unsynced = _events.Values.Where(v => !v.Synced).Select(v => v.Event);
            return Task.FromResult<IReadOnlyList<GameEvent>>(ProgressModel.Order(unsynced).Take(max).ToList());
        }
    }

    public Task MarkSyncedAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default)
    {
        lock (_gate)
        {
            foreach (Guid id in eventIds)
            {
                if (_events.TryGetValue(id, out var entry))
                    _events[id] = (entry.Event, true);
            }
        }

        return Task.CompletedTask;
    }

    public Task MarkAllUnsyncedAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            foreach (Guid id in _events.Keys.ToList())
                _events[id] = (_events[id].Event, false);
        }

        return Task.CompletedTask;
    }

    public Task<int> AddFromServerAsync(IEnumerable<GameEvent> events, CancellationToken ct = default)
    {
        int added = 0;
        lock (_gate)
        {
            foreach (GameEvent e in events)
            {
                if (_events.TryAdd(e.Id, (e, true)))
                    added++;
            }
        }

        return Task.FromResult(added);
    }

    public Task<string?> GetValueAsync(string key, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
    }

    public Task SetValueAsync(string key, string value, CancellationToken ct = default)
    {
        lock (_gate)
            _values[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveValueAsync(string key, CancellationToken ct = default)
    {
        lock (_gate)
            _values.Remove(key);
        return Task.CompletedTask;
    }

    public Task DeleteAllAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            _events.Clear();
            _values.Clear();
        }

        return Task.CompletedTask;
    }
}
