using System;
using System.Linq;
using Matikka.Core.Games;
using Matikka.Core.Progress;
using Matikka.Core.Randomness;

namespace Matikka.Core.Adaptivity
{
    /// <summary>Valitsee seuraavan tason taitoluvun perusteella ja päättää, mitkä tasot ovat auki.</summary>
    public static class LevelSelector
    {
        /// <summary>Osuus tehtävistä, joissa tarjotaan tarkoituksella helpompi kertaustehtävä.</summary>
        public const double ReviewChance = 0.2;

        /// <summary>Tasot aukeavat yksi kerrallaan: korkein ratkaistu + 1.</summary>
        public static int HighestUnlocked(GameInfo game, ProgressModel progress) =>
            Math.Min(game.Levels.Count, progress.HighestSolvedLevel(game) + 1);

        public static bool IsUnlocked(GameInfo game, ProgressModel progress, int level) =>
            level >= 1 && level <= HighestUnlocked(game, progress);

        /// <summary>
        /// Avoimista tasoista se, jonka odotettu onnistuminen on lähimpänä 75 %:a.
        /// Joka viides kerta (keskimäärin) taso alemmas: kertaus pitää onnistumisen kokemuksen yllä.
        /// </summary>
        public static int ChooseNext(GameInfo game, ProgressModel progress, SeededRandom random)
        {
            double rating = progress.GetSkill(game).Rating;
            int unlocked = HighestUnlocked(game, progress);

            LevelInfo best = game.Levels
                .Where(l => l.Number <= unlocked)
                .OrderBy(l => Math.Abs(EloRating.Expected(rating, l.Difficulty) - EloRating.TargetSuccess))
                .ThenBy(l => l.Number)
                .First();

            if (best.Number > 1 && random.NextDouble() < ReviewChance)
                return best.Number - 1;

            return best.Number;
        }
    }
}
