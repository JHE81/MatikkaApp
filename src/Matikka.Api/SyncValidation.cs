using Matikka.Core.Progress;
using Matikka.Core.Sync;

namespace Matikka.Api;

/// <summary>Hylkää selvästi virheelliset tapahtumat ennen tallennusta.</summary>
public static class SyncValidation
{
    private static readonly DateTimeOffset EarliestValid = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static Dictionary<string, string[]> Validate(SyncRequest request, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.SinceSeq < 0)
            errors["sinceSeq"] = ["Ei voi olla negatiivinen."];

        if (request.Events.Count > SyncRequest.MaxEventsPerRequest)
        {
            errors["events"] = [$"Enintään {SyncRequest.MaxEventsPerRequest} tapahtumaa kerralla."];
            return errors;
        }

        for (int i = 0; i < request.Events.Count; i++)
        {
            string? problem = Check(request.Events[i], now);
            if (problem != null)
                errors[$"events[{i}]"] = [problem];
        }

        return errors;
    }

    private static string? Check(GameEvent e, DateTimeOffset now)
    {
        if (e.Id == Guid.Empty) return "Id puuttuu.";
        if (string.IsNullOrWhiteSpace(e.GameId) || e.GameId.Length > 64) return "GameId puuttuu tai on liian pitkä.";
        if (e.SkillId.Length > 64) return "SkillId on liian pitkä.";
        if (e.Level is < 1 or > 100) return "Level on virheellinen.";
        if (e.Stars is < 0 or > 3) return "Stars on virheellinen.";
        if (e.Moves is < 0 or > 10_000 || e.Par is < 0 or > 100 || e.HintsUsed is < 0 or > 100) return "Siirtomäärät ovat virheellisiä.";
        if (e.DurationMs is < 0 or > 24L * 60 * 60 * 1000) return "DurationMs on virheellinen.";
        if (e.OccurredAt < EarliestValid || e.OccurredAt > now.AddDays(1)) return "OccurredAt on virheellinen.";
        if (!Enum.IsDefined(e.Outcome)) return "Outcome on virheellinen.";
        return null;
    }
}
