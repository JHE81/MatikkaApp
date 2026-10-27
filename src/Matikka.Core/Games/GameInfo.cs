using System;
using System.Collections.Generic;
using System.Linq;

namespace Matikka.Core.Games
{
    /// <summary>
    /// Pelin kuvaus: tunniste, taitoalue johon peli kuuluu (OPS-sisältö) ja tasot.
    /// Uusi peli = uusi GameInfo + oma pelilogiikka + oma näkymä. Edistyminen, Elo ja synkronointi
    /// toimivat kaikille peleille samalla tavalla.
    /// </summary>
    public sealed class GameInfo
    {
        public GameInfo(string gameId, string skillId, string title, IReadOnlyList<LevelInfo> levels)
        {
            if (levels.Count == 0)
                throw new ArgumentException("Pelillä on oltava vähintään yksi taso.", nameof(levels));

            GameId = gameId;
            SkillId = skillId;
            Title = title;
            Levels = levels;
        }

        /// <summary>Pysyvä tunniste. Tallentuu tapahtumiin, joten älä muuta julkaisun jälkeen.</summary>
        public string GameId { get; }

        /// <summary>Taitoalue, jolle pelaajan taitoluku lasketaan, esim. "algebra.yhtalot".</summary>
        public string SkillId { get; }

        public string Title { get; }

        public IReadOnlyList<LevelInfo> Levels { get; }

        /// <summary>
        /// Uuden pelaajan taitoluku: valittu niin, että ensimmäisellä tasolla onnistumisen
        /// todennäköisyys on tavoitetasolla (75 %).
        /// </summary>
        public double InitialRating => Levels[0].Difficulty + Adaptivity.EloRating.TargetLogit;

        public LevelInfo GetLevel(int number)
        {
            LevelInfo? level = Levels.FirstOrDefault(l => l.Number == number);
            if (level == null)
                throw new ArgumentOutOfRangeException(nameof(number), number, "Tuntematon taso.");
            return level;
        }
    }

    /// <param name="Number">Tason numero, alkaen 1:stä.</param>
    /// <param name="Name">Lyhyt nimi pelaajalle.</param>
    /// <param name="Description">Mitä tasolla harjoitellaan (näytetään myös vanhemmalle).</param>
    /// <param name="Difficulty">Tason vaikeus Elo-asteikolla (logit-yksikköinä).</param>
    public sealed record LevelInfo(int Number, string Name, string Description, double Difficulty);
}
