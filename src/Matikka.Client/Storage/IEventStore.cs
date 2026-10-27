using Matikka.Core.Progress;

namespace Matikka.Client.Storage;

/// <summary>
/// Laitteen paikallinen tallennus. Tapahtumat ovat totuus; avain-arvo-pari on pieniä
/// asetuksia varten (profiili, synkronoinnin kohta).
/// </summary>
public interface IEventStore
{
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Tallentaa uuden, vielä synkronoimattoman tapahtuman. Sama Id kahdesti ei tee mitään.</summary>
    Task AppendAsync(GameEvent gameEvent, CancellationToken ct = default);

    Task<IReadOnlyList<GameEvent>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<GameEvent>> GetUnsyncedAsync(int max, CancellationToken ct = default);

    Task MarkSyncedAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default);

    /// <summary>Merkitsee kaikki tapahtumat lähetettäviksi uudelleen (esim. uusi palvelintili).</summary>
    Task MarkAllUnsyncedAsync(CancellationToken ct = default);

    /// <summary>Lisää palvelimelta tulleet tapahtumat. Palauttaa uusien määrän.</summary>
    Task<int> AddFromServerAsync(IEnumerable<GameEvent> events, CancellationToken ct = default);

    Task<string?> GetValueAsync(string key, CancellationToken ct = default);

    Task SetValueAsync(string key, string value, CancellationToken ct = default);

    Task RemoveValueAsync(string key, CancellationToken ct = default);

    /// <summary>Poistaa kaiken (tilin poisto / aloitus alusta).</summary>
    Task DeleteAllAsync(CancellationToken ct = default);
}
