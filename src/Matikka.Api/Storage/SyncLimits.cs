namespace Matikka.Api.Storage;

public static class SyncLimits
{
    /// <summary>Enintään näin monta tapahtumaa palautetaan kerralla. Laite hakee loput seuraavalla kerralla.</summary>
    public const int MaxIncoming = 1000;
}
