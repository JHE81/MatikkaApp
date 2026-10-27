using System.Text.Json;
using Matikka.Core.Progress;
using Matikka.Core.Sync;
using Microsoft.EntityFrameworkCore;

namespace Matikka.Api.Storage.Postgres;

public sealed class PostgresSyncStore(MatikkaDbContext db, TimeProvider clock) : ISyncStore
{
    public async Task<Guid> CreateAccountAsync(string tokenHash, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var account = new AccountRow { Id = Guid.NewGuid(), TokenHash = tokenHash, CreatedAt = now, LastSeenAt = now };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account.Id;
    }

    public async Task<Guid?> FindAccountAsync(string tokenHash, CancellationToken ct) =>
        await db.Accounts
            .Where(a => a.TokenHash == tokenHash)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<SyncResponse> SyncAsync(Guid accountId, SyncRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Lukitaan tilin rivi: saman tilin rinnakkaiset synkronoinnit (kaksi laitetta) käsitellään
        // vuorotellen. Silloin järjestysnumerot valmistuvat kasvavassa järjestyksessä eikä
        // yksikään tapahtuma jää laitteelta saamatta.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM accounts WHERE id = {accountId} FOR UPDATE", ct);

        var submittedIds = request.Events.Select(e => e.Id).Distinct().ToList();
        var existing = await db.Events
            .Where(e => e.AccountId == accountId && submittedIds.Contains(e.EventId))
            .Select(e => e.EventId)
            .ToListAsync(ct);
        var known = existing.ToHashSet();

        DateTimeOffset now = clock.GetUtcNow();
        foreach (GameEvent e in request.Events)
        {
            if (!known.Add(e.Id))
                continue;

            db.Events.Add(new EventRow
            {
                AccountId = accountId,
                EventId = e.Id,
                GameId = e.GameId,
                Level = e.Level,
                OccurredAt = e.OccurredAt.ToUniversalTime(),
                ReceivedAt = now,
                Payload = JsonSerializer.Serialize(e, ApiJson.Options),
            });
        }

        await db.Accounts
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.LastSeenAt, now), ct);
        await db.SaveChangesAsync(ct);

        var incomingRows = await db.Events
            .Where(e => e.AccountId == accountId && e.Seq > request.SinceSeq && !submittedIds.Contains(e.EventId))
            .OrderBy(e => e.Seq)
            .Take(SyncLimits.MaxIncoming)
            .Select(e => new { e.Seq, e.Payload })
            .ToListAsync(ct);

        bool hasMore = incomingRows.Count == SyncLimits.MaxIncoming;
        long latest = hasMore
            ? incomingRows[^1].Seq
            : await db.Events.Where(e => e.AccountId == accountId).MaxAsync(e => (long?)e.Seq, ct) ?? 0;

        await transaction.CommitAsync(ct);

        return new SyncResponse
        {
            AcceptedIds = submittedIds,
            Incoming = incomingRows
                .Select(r => JsonSerializer.Deserialize<GameEvent>(r.Payload, ApiJson.Options)!)
                .ToList(),
            LatestSeq = latest,
            HasMore = hasMore,
        };
    }

    public async Task DeleteAccountAsync(Guid accountId, CancellationToken ct)
    {
        // Tapahtumat poistuvat kaskadina.
        await db.Accounts.Where(a => a.Id == accountId).ExecuteDeleteAsync(ct);
    }
}
