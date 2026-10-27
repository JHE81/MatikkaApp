using Matikka.Client.Profile;
using Matikka.Client.Progress;
using Matikka.Client.Storage;
using Matikka.Client.Sync;
using Matikka.DevHost.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Samat palvelut kuin MAUI-sovelluksessa (MauiProgram.cs). Tietokanta on projektikansiossa,
// joten edistyminen säilyy uudelleenkäynnistysten yli kuten puhelimessa.
string dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDir);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEventStore>(_ => new SqliteEventStore(Path.Combine(dataDir, "matikka-dev.db")));
builder.Services.AddSingleton<ISecretStore, DevelopmentSecretStore>();
builder.Services.AddSingleton<ProgressService>();
builder.Services.AddSingleton<ProfileService>();
builder.Services.AddSingleton<AppearanceService>();
builder.Services.AddSingleton<CharacterService>();

// Api:BaseUrl puuttuu → synkronointi pois päältä, peli toimii pelkällä paikallisella tallennuksella.
string? apiBase = builder.Configuration["Api:BaseUrl"];
builder.Services.AddSingleton(sp => new SyncService(
    string.IsNullOrWhiteSpace(apiBase) ? null : new HttpClient { BaseAddress = new Uri(apiBase), Timeout = TimeSpan.FromSeconds(15) },
    sp.GetRequiredService<IEventStore>(),
    sp.GetRequiredService<ISecretStore>(),
    sp.GetRequiredService<ProgressService>(),
    sp.GetRequiredService<ProfileService>()));

var app = builder.Build();

app.Services.GetRequiredService<SyncService>().Start();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Matikka.Ui.Routes).Assembly);

app.Run();
