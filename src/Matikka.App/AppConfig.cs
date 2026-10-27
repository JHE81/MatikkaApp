namespace Matikka.App;

internal static class AppConfig
{
    /// <summary>
    /// Synkronointipalvelimen osoite. Kehityksessä Android-emulaattori näkee koneen
    /// localhostin osoitteessa 10.0.2.2, iOS-simulaattori suoraan localhostina.
    /// Julkaisuversioon tuotanto-osoite (https). Null = ei synkronointia.
    /// </summary>
    public static Uri? ApiBaseUrl =>
#if DEBUG && ANDROID
        new("http://10.0.2.2:5080/");
#elif DEBUG
        new("http://localhost:5080/");
#else
        null; // TODO: new("https://api.oma-domain.fi/") kun palvelin on pystyssä
#endif
}
