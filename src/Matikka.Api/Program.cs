using System.Threading.RateLimiting;
using Matikka.Api;
using Matikka.Api.Storage;
using Matikka.Api.Storage.Postgres;
using Matikka.Core.Sync;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o => ApiJson.Configure(o.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

// Tallennus: PostgreSQL, jos yhteysmerkkijono on annettu, muuten muisti (paikallinen kehitys).
string? connectionString = builder.Configuration.GetConnectionString("Matikka");
if (string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddSingleton<ISyncStore, InMemorySyncStore>();
}
else
{
    builder.Services.AddDbContext<MatikkaDbContext>(o => o.UseNpgsql(connectionString));
    builder.Services.AddScoped<ISyncStore, PostgresSyncStore>();
}

// Pilvessä (Azure Container Apps, Hetzner + reverse proxy) asiakkaan IP tulee X-Forwarded-For-otsakkeessa.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Tilien luonti: rajoitetaan IP:n mukaan, ettei tietokantaa täytetä roskatileillä.
    o.AddPolicy("accounts", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromHours(1) }));

    // Synkronointi: rajoitetaan tokenin mukaan.
    o.AddPolicy("sync", http => RateLimitPartition.GetFixedWindowLimiter(
        Tokens.FromRequest(http.Request) ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseRateLimiter();

if (!string.IsNullOrWhiteSpace(connectionString))
{
    // Demovaiheen yksinkertaistus: taulut luodaan käynnistyksessä. Ennen tuotantoa siirry
    // EF Core -migraatioihin (dotnet ef migrations add Initial).
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<MatikkaDbContext>().Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api");

api.MapPost("/accounts", async (ISyncStore store, CancellationToken ct) =>
{
    string token = Tokens.Create();
    Guid id = await store.CreateAccountAsync(Tokens.Hash(token), ct);
    return Results.Ok(new AccountCreatedResponse { AccountId = id, Token = token });
})
.RequireRateLimiting("accounts");

api.MapPost("/sync", async (HttpRequest http, SyncRequest body, ISyncStore store, TimeProvider clock, CancellationToken ct) =>
{
    Guid? account = await AuthenticateAsync(http, store, ct);
    if (account == null)
        return Results.Unauthorized();

    var errors = SyncValidation.Validate(body, clock.GetUtcNow());
    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    SyncResponse response = await store.SyncAsync(account.Value, body, ct);
    return Results.Ok(response);
})
.RequireRateLimiting("sync");

// Tilin ja kaikkien tietojen poisto (App Store vaatii tämän sovelluksen sisältä).
api.MapDelete("/accounts/me", async (HttpRequest http, ISyncStore store, CancellationToken ct) =>
{
    Guid? account = await AuthenticateAsync(http, store, ct);
    if (account == null)
        return Results.Unauthorized();

    await store.DeleteAccountAsync(account.Value, ct);
    return Results.NoContent();
})
.RequireRateLimiting("sync");

app.Run();

static async Task<Guid?> AuthenticateAsync(HttpRequest request, ISyncStore store, CancellationToken ct)
{
    string? token = Tokens.FromRequest(request);
    return token == null ? null : await store.FindAccountAsync(Tokens.Hash(token), ct);
}

/// <summary>Testien WebApplicationFactorya varten.</summary>
public partial class Program;
