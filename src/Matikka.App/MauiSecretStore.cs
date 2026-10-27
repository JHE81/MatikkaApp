using Matikka.Client.Storage;

namespace Matikka.App;

/// <summary>Tunnistetoken iOS Keychainiin / Android Keystoreen.</summary>
internal sealed class MauiSecretStore : ISecretStore
{
    public Task<string?> GetAsync(string key) => SecureStorage.Default.GetAsync(key);

    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);

    public Task RemoveAsync(string key)
    {
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }
}
