using Matikka.Core.Sync;

namespace Matikka.Api.Storage;

public interface ISyncStore
{
    /// <summary>Luo nimettömän tilin. Tallennetaan vain tokenin tiiviste, ei itse tokenia.</summary>
    Task<Guid> CreateAccountAsync(string tokenHash, CancellationToken ct);

    Task<Guid?> FindAccountAsync(string tokenHash, CancellationToken ct);

    /// <summary>
    /// Tallentaa uudet tapahtumat (idempotentti: sama Id kahdesti ei tee mitään) ja palauttaa
    /// tapahtumat, joiden järjestysnumero on SinceSeq:iä suurempi ja joita pyynnössä ei ollut.
    /// </summary>
    Task<SyncResponse> SyncAsync(Guid accountId, SyncRequest request, CancellationToken ct);

    /// <summary>Poistaa tilin ja kaikki sen tapahtumat.</summary>
    Task DeleteAccountAsync(Guid accountId, CancellationToken ct);
}
