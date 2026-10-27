using System;

namespace Matikka.Core.Progress
{
    public enum GameOutcome
    {
        Solved = 0,

        /// <summary>Pelaaja ohitti tehtävän. Lasketaan epäonnistumiseksi taitoluvussa.</summary>
        Skipped = 1,
    }

    /// <summary>
    /// Yksi pelattu tehtävä. Tämä on ainoa asia, joka tallennetaan ja synkronoidaan:
    /// kaikki muu (taitoluvut, tähdet, putket) lasketaan tapahtumista. Tapahtumat ovat
    /// muuttumattomia ja niillä on yksilöllinen Id, joten synkronointi on idempotentti.
    ///
    /// Tietosuoja: tapahtumassa ei ole nimeä, ikää eikä laitetietoja.
    /// </summary>
    public sealed record GameEvent
    {
        public Guid Id { get; init; }

        /// <summary>Kasvatetaan, jos kenttiin tulee merkitysmuutoksia.</summary>
        public int SchemaVersion { get; init; } = 1;

        public string GameId { get; init; } = "";

        public string SkillId { get; init; } = "";

        public int Level { get; init; }

        /// <summary>Tehtävän siemen: taso + siemen toistaa saman tehtävän.</summary>
        public long Seed { get; init; }

        public GameOutcome Outcome { get; init; }

        public int Moves { get; init; }

        public int Par { get; init; }

        public int HintsUsed { get; init; }

        /// <summary>0–3. Ohitetussa tehtävässä 0.</summary>
        public int Stars { get; init; }

        public long DurationMs { get; init; }

        /// <summary>Laitteen paikallinen aika aikavyöhykkeineen (päiväkohtaisia tilastoja varten).</summary>
        public DateTimeOffset OccurredAt { get; init; }
    }
}
