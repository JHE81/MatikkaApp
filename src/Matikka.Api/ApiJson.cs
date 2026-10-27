using System.Text.Json;
using System.Text.Json.Serialization;

namespace Matikka.Api;

/// <summary>Sama JSON-muoto kuin sovelluksen ClientJsonContextissa: camelCase ja enumit merkkijonoina.</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
