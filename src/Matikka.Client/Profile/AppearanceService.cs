using Matikka.Client.Storage;

namespace Matikka.Client.Profile;

/// <summary>Värimaailma, jonka pelaaja valitsee. Laitekohtainen asetus, ei synkronoidu.</summary>
public sealed record ThemeOption(string Id, string Name);

public sealed class AppearanceService(IEventStore store)
{
    private const string Key = "theme";

    /// <summary>Teemojen tunnisteet vastaavat CSS:n [data-theme]-arvoja (wwwroot/app.css).</summary>
    public static readonly IReadOnlyList<ThemeOption> Themes =
    [
        new("minttu", "Minttu"),
        new("laguuni", "Laguuni"),
        new("vadelma", "Vadelma"),
        new("yo", "Yö"),
    ];

    public static string DefaultTheme => Themes[0].Id;

    private string? _theme;

    public event Action? Changed;

    public async Task<string> GetThemeAsync(CancellationToken ct = default)
    {
        if (_theme != null)
            return _theme;

        string? stored = await store.GetValueAsync(Key, ct);
        _theme = Themes.Any(t => t.Id == stored) ? stored! : DefaultTheme;
        return _theme;
    }

    public async Task SetThemeAsync(string themeId, CancellationToken ct = default)
    {
        if (Themes.All(t => t.Id != themeId))
            throw new ArgumentException($"Tuntematon teema: {themeId}", nameof(themeId));

        await store.SetValueAsync(Key, themeId, ct);
        _theme = themeId;
        Changed?.Invoke();
    }
}
