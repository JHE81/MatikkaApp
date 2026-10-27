using System.Security.Cryptography;
using System.Text;

namespace Matikka.Api;

/// <summary>
/// Laitteen tunnistetoken: 256 bittiä satunnaisuutta. Palvelin tallentaa vain SHA-256-tiivisteen,
/// joten tietokantavuoto ei paljasta toimivia tokeneita. Hidasta salasanatiivistettä ei tarvita,
/// koska token ei ole ihmisen keksimä eikä sitä voi arvata.
/// </summary>
public static class Tokens
{
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>Lukee "Authorization: Bearer ..." -otsakkeen. Null, jos puuttuu tai on väärän muotoinen.</summary>
    public static string? FromRequest(HttpRequest request)
    {
        string? header = request.Headers.Authorization;
        const string prefix = "Bearer ";
        if (header == null || !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        string token = header[prefix.Length..].Trim();
        return token.Length is > 0 and <= 128 ? token : null;
    }
}
