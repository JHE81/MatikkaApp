using System.Text.Json;
using System.Text.Json.Serialization;
using Matikka.Client.Profile;
using Matikka.Core.Progress;
using Matikka.Core.Sync;

namespace Matikka.Client.Storage;

/// <summary>
/// Käännösaikana generoitu JSON-sarjallistus. Pakollinen iOS:llä, jossa julkaisuversio
/// karsitaan ja heijastukseen perustuva System.Text.Json on oletuksena pois päältä.
/// Enumit kirjoitetaan merkkijonoina, jotta palvelimen data on luettavaa.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true)]
[JsonSerializable(typeof(GameEvent))]
[JsonSerializable(typeof(SyncRequest))]
[JsonSerializable(typeof(SyncResponse))]
[JsonSerializable(typeof(AccountCreatedResponse))]
[JsonSerializable(typeof(PlayerProfile))]
internal partial class ClientJsonContext : JsonSerializerContext
{
}
