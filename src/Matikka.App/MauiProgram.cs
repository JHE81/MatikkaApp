using Matikka.Client.Profile;
using Matikka.Client.Progress;
using Matikka.Client.Storage;
using Matikka.Client.Sync;
using Microsoft.Extensions.Logging;

namespace Matikka.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Offline-first: tietokanta laitteen sovelluskansiossa. Varmuuskopioituu iCloudiin /
        // Google-varmuuskopioon käyttöjärjestelmän asetusten mukaan.
        string databasePath = Path.Combine(FileSystem.AppDataDirectory, "matikka.db");

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IEventStore>(_ => new SqliteEventStore(databasePath));
        builder.Services.AddSingleton<ISecretStore, MauiSecretStore>();
        builder.Services.AddSingleton<ProgressService>();
        builder.Services.AddSingleton<ProfileService>();
        builder.Services.AddSingleton<AppearanceService>();
        builder.Services.AddSingleton<CharacterService>();
        builder.Services.AddSingleton(sp => new SyncService(
            CreateApiClient(),
            sp.GetRequiredService<IEventStore>(),
            sp.GetRequiredService<ISecretStore>(),
            sp.GetRequiredService<ProgressService>(),
            sp.GetRequiredService<ProfileService>()));

        return builder.Build();
    }

    /// <summary>Null = synkronointi pois päältä (sovellus toimii silti täysin).</summary>
    private static HttpClient? CreateApiClient()
    {
        Uri? baseAddress = AppConfig.ApiBaseUrl;
        return baseAddress == null
            ? null
            : new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(15) };
    }
}
