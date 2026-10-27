using Matikka.Client.Storage;

namespace Matikka.Client.Profile;

public sealed record CharacterOption(string Id, string Name);

/// <summary>
/// Pelaajan hahmo. Laitekohtainen valinta. Myöhemmin tähän tulevat kolikoilla avattavat
/// hahmot: lista laajenee ja valinta tarkistaa, onko hahmo avattu.
/// </summary>
public sealed class CharacterService(IEventStore store)
{
    private const string Key = "character";

    /// <summary>Tunnisteet vastaavat käyttöliittymän CharacterAvatar-komponentin piirroksia.</summary>
    public static readonly IReadOnlyList<CharacterOption> Characters =
    [
        new("napsu", "Napsu"),
        new("moykky", "Möykky"),
        new("porri", "Pörri"),
        new("bitti", "Bitti"),
    ];

    public static string DefaultCharacter => Characters[0].Id;

    private string? _current;

    public event Action? Changed;

    public async Task<CharacterOption> GetAsync(CancellationToken ct = default)
    {
        if (_current == null)
        {
            string? stored = await store.GetValueAsync(Key, ct);
            _current = Characters.Any(c => c.Id == stored) ? stored! : DefaultCharacter;
        }

        return Characters.First(c => c.Id == _current);
    }

    public async Task SetAsync(string id, CancellationToken ct = default)
    {
        if (Characters.All(c => c.Id != id))
            throw new ArgumentException($"Tuntematon hahmo: {id}", nameof(id));

        await store.SetValueAsync(Key, id, ct);
        _current = id;
        Changed?.Invoke();
    }
}
