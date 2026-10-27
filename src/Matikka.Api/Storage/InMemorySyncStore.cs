using Matikka.Core.Progress;
using Matikka.Core.Sync;

namespace Matikka.Api.Storage;

/// <summary>
/// Muistivarasto paikalliseen kehitykseen ja testeihin. Tiedot katoavat uudelleenkäynnistyksessä.
/// Noudattaa samoja sääntöjä kuin PostgreSQL-toteutus.
/// </summary>
public sealed class InMemorySyncStore : ISyncStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Guid> _tokens = new();
    private readonly List<(long Seq, Guid Account, GameEvent Event)> _events = new();
    private long _seq;

    public Task<Guid> CreateAccountAsync(string tokenHash, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        lock (_gate)
            _tokens[tokenHash] = id;
        return Task.FromResult(id);
    }

    public Task<Guid?> FindAccountAsync(string tokenHash, CancellationToken ct)
    {
        lock (_gate)
            return Task.FromResult(_tokens.TryGetValue(tokenHash, out Guid id) ? id : (Guid?)null);
    }

    public Task<SyncResponse> SyncAsync(Guid accountId, SyncRequest request, CancellationToken ct)
    {
        lock (_gate)
        {
            var submitted = request.Events.Select(e => e.Id).ToHashSet();
            var existing = _events.Where(e => e.Account == accountId).Select(e => e.Event.Id).ToHashSet();

            foreach (GameEvent e in request.Events)
            {
                if (existing.Add(e.Id))
                    _events.Add((++_seq, accountId, e));
            }

            var incoming = _events
                .Where(e => e.Account == accountId && e.Seq > request.SinceSeq && !submitted.Contains(e.Event.Id))
                .OrderBy(e => e.Seq)
                .Take(SyncLimits.MaxIncoming)
                .ToList();

            long latest = incoming.Count == SyncLimits.MaxIncoming
                ? incoming[^1].Seq
                : _events.Where(e => e.Account == accountId).Select(e => e.Seq).DefaultIfEmpty(0).Max();

            return Task.FromResult(new SyncResponse
            {
                AcceptedIds = submitted.ToList(),
                Incoming = incoming.Select(e => e.Event).ToList(),
                LatestSeq = latest,
                HasMore = incoming.Count == SyncLimits.MaxIncoming,
            });
        }
    }

    public Task DeleteAccountAsync(Guid accountId, CancellationToken ct)
    {
        lock (_gate)
        {
            foreach (var token in _tokens.Where(t => t.Value == accountId).Select(t => t.Key).ToList())
                _tokens.Remove(token);
            _events.RemoveAll(e => e.Account == accountId);
        }

        return Task.CompletedTask;
    }
}
