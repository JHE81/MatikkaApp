using System;
using System.Collections.Generic;
using System.Linq;
using Matikka.Core.Adaptivity;
using Matikka.Core.Games;

namespace Matikka.Core.Progress
{
    public static class Scoring
    {
        /// <summary>Muuttaa pelituloksen Elo-pisteeksi 0–1.</summary>
        public static double ToScore(GameEvent e)
        {
            if (e.Outcome == GameOutcome.Skipped)
                return 0.0;

            switch (e.Stars)
            {
                case 3: return 1.0;
                case 2: return 0.8;
                default: return 0.6;
            }
        }
    }

    public sealed class SkillState
    {
        public SkillState(double rating, int attempts)
        {
            Rating = rating;
            Attempts = attempts;
        }

        public double Rating { get; internal set; }

        public int Attempts { get; internal set; }
    }

    public sealed class LevelStats
    {
        public int Attempts { get; internal set; }

        public int Solved { get; internal set; }

        public int BestStars { get; internal set; }
    }

    /// <summary>
    /// Pelaajan edistyminen laskettuna tapahtumista. Sama tapahtumajoukko tuottaa aina saman
    /// tuloksen riippumatta siitä, missä järjestyksessä tapahtumat saapuivat laitteelle,
    /// koska ne käsitellään aikajärjestyksessä. Siksi kahden laitteen tiedot voi yhdistää
    /// pelkästään tapahtumat yhdistämällä.
    /// </summary>
    public sealed class ProgressModel
    {
        private readonly HashSet<Guid> _applied = new HashSet<Guid>();
        private readonly Dictionary<string, SkillState> _skills = new Dictionary<string, SkillState>();
        private readonly Dictionary<string, LevelStats> _levels = new Dictionary<string, LevelStats>();
        private readonly Dictionary<DateTime, int> _solvedPerDay = new Dictionary<DateTime, int>();
        private GameEvent? _last;

        public int TotalSolved { get; private set; }

        public int TotalStars { get; private set; }

        public int EventCount => _applied.Count;

        public IReadOnlyDictionary<DateTime, int> SolvedPerDay => _solvedPerDay;

        public static ProgressModel Replay(IEnumerable<GameEvent> events)
        {
            var model = new ProgressModel();
            foreach (GameEvent e in Order(events))
                model.ApplyInternal(e);
            return model;
        }

        /// <summary>Järjestys, jossa tapahtumat käsitellään. Sama kaikilla laitteilla.</summary>
        public static IEnumerable<GameEvent> Order(IEnumerable<GameEvent> events) =>
            events.OrderBy(e => e.OccurredAt.UtcTicks).ThenBy(e => e.Id);

        /// <summary>
        /// Lisää uuden tapahtuman. Jos tapahtuma on vanhempi kuin jo käsitellyt (esim. toiselta
        /// laitteelta synkronoitu), palauttaa false: silloin kutsujan pitää laskea malli uudelleen
        /// <see cref="Replay"/>-metodilla, jotta järjestys säilyy.
        /// </summary>
        public bool TryAppend(GameEvent e)
        {
            if (_applied.Contains(e.Id))
                return true;

            if (_last != null && Compare(e, _last) < 0)
                return false;

            ApplyInternal(e);
            return true;
        }

        public bool Contains(Guid eventId) => _applied.Contains(eventId);

        public SkillState GetSkill(GameInfo game)
        {
            SkillState? state;
            return _skills.TryGetValue(game.SkillId, out state)
                ? state
                : new SkillState(game.InitialRating, 0);
        }

        public LevelStats GetLevel(string gameId, int level)
        {
            LevelStats? stats;
            return _levels.TryGetValue(Key(gameId, level), out stats) ? stats : new LevelStats();
        }

        /// <summary>Korkein ratkaistu taso pelissä, 0 jos ei yhtään.</summary>
        public int HighestSolvedLevel(GameInfo game) =>
            game.Levels.Where(l => GetLevel(game.GameId, l.Number).Solved > 0)
                .Select(l => l.Number)
                .DefaultIfEmpty(0)
                .Max();

        /// <summary>Peräkkäiset päivät, joina on ratkaistu vähintään yksi tehtävä, päättyen tänään tai eilen.</summary>
        public int CurrentStreak(DateTime today)
        {
            DateTime day = today.Date;
            if (!_solvedPerDay.ContainsKey(day))
                day = day.AddDays(-1);

            int streak = 0;
            while (_solvedPerDay.ContainsKey(day))
            {
                streak++;
                day = day.AddDays(-1);
            }

            return streak;
        }

        private void ApplyInternal(GameEvent e)
        {
            if (!_applied.Add(e.Id))
                return;

            _last = _last == null || Compare(e, _last) > 0 ? e : _last;

            LevelStats? level;
            string key = Key(e.GameId, e.Level);
            if (!_levels.TryGetValue(key, out level))
            {
                level = new LevelStats();
                _levels[key] = level;
            }

            level.Attempts++;
            if (e.Outcome == GameOutcome.Solved)
            {
                level.Solved++;
                level.BestStars = Math.Max(level.BestStars, e.Stars);
                TotalSolved++;
                TotalStars += e.Stars;

                DateTime day = e.OccurredAt.Date;
                int count;
                _solvedPerDay.TryGetValue(day, out count);
                _solvedPerDay[day] = count + 1;
            }

            // Tuntemattoman pelin tai tason tapahtuma (uudempi sovellusversio) ei päivitä
            // taitolukua, mutta tallentuu ja synkronoituu silti.
            GameInfo? game = GameCatalog.Find(e.GameId);
            LevelInfo? levelInfo = game?.Levels.FirstOrDefault(l => l.Number == e.Level);
            if (game == null || levelInfo == null || game.SkillId != e.SkillId)
                return;

            SkillState skill = GetSkill(game);
            double newRating = EloRating.Update(skill.Rating, levelInfo.Difficulty, Scoring.ToScore(e), skill.Attempts);
            _skills[game.SkillId] = new SkillState(newRating, skill.Attempts + 1);
        }

        private static int Compare(GameEvent a, GameEvent b)
        {
            int byTime = a.OccurredAt.UtcTicks.CompareTo(b.OccurredAt.UtcTicks);
            return byTime != 0 ? byTime : a.Id.CompareTo(b.Id);
        }

        private static string Key(string gameId, int level) => gameId + "#" + level;
    }
}
