namespace Matikka.Client.Storage;

/// <summary>
/// Salaisuuksien tallennus (palvelimen tunnistetoken). MAUIssa toteutus käyttää
/// SecureStoragea (iOS Keychain / Android Keystore).
/// </summary>
public interface ISecretStore
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    Task RemoveAsync(string key);
}

/// <summary>
/// Kehityskäyttöön: tallentaa salaisuuden tavalliseen avain-arvo-tauluun.
/// Älä käytä julkaistavassa sovelluksessa.
/// </summary>
public sealed class DevelopmentSecretStore(IEventStore store) : ISecretStore
{
    private const string Prefix = "secret:";

    public Task<string?> GetAsync(string key) => store.GetValueAsync(Prefix + key);

    public Task SetAsync(string key, string value) => store.SetValueAsync(Prefix + key, value);

    public Task RemoveAsync(string key) => store.RemoveValueAsync(Prefix + key);
}
