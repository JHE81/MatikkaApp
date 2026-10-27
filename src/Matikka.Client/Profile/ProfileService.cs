using System.Text.Json;
using Matikka.Client.Storage;

namespace Matikka.Client.Profile;

/// <summary>
/// Pelaajan profiili. Tallennetaan VAIN laitteelle: palvelin ei saa nimimerkkiä eikä
/// luokka-astetta. Näin palvelimella ei ole henkilötietoja, ainoastaan nimetön tili ja pelitapahtumat.
/// </summary>
public sealed record PlayerProfile
{
    public string Nickname { get; init; } = "";

    /// <summary>7, 8 tai 9.</summary>
    public int Grade { get; init; } = 7;

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class ProfileService(IEventStore store, TimeProvider clock)
{
    public const int MaxNicknameLength = 16;
    private const string Key = "profile";

    private PlayerProfile? _cached;
    private bool _loaded;

    public event Action? Changed;

    public async Task<PlayerProfile?> GetAsync(CancellationToken ct = default)
    {
        if (_loaded)
            return _cached;

        string? json = await store.GetValueAsync(Key, ct);
        _cached = json == null ? null : JsonSerializer.Deserialize(json, ClientJsonContext.Default.PlayerProfile);
        _loaded = true;
        return _cached;
    }

    public async Task<PlayerProfile> SaveAsync(string nickname, int grade, CancellationToken ct = default)
    {
        nickname = nickname.Trim();
        if (nickname.Length == 0)
            throw new ArgumentException("Nimimerkki puuttuu.", nameof(nickname));
        if (nickname.Length > MaxNicknameLength)
            nickname = nickname[..MaxNicknameLength];
        if (grade is < 7 or > 9)
            throw new ArgumentOutOfRangeException(nameof(grade), grade, "Luokka-asteen on oltava 7–9.");

        PlayerProfile? existing = await GetAsync(ct);
        var profile = new PlayerProfile
        {
            Nickname = nickname,
            Grade = grade,
            CreatedAt = existing?.CreatedAt ?? clock.GetLocalNow(),
        };

        await store.SetValueAsync(Key, JsonSerializer.Serialize(profile, ClientJsonContext.Default.PlayerProfile), ct);
        _cached = profile;
        _loaded = true;
        Changed?.Invoke();
        return profile;
    }

    /// <summary>Unohdetaan välimuisti, kun kaikki tiedot on poistettu.</summary>
    internal void Reset()
    {
        _cached = null;
        _loaded = false;
        Changed?.Invoke();
    }
}
