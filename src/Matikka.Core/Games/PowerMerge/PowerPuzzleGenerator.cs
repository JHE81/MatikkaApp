using System;
using System.Collections.Generic;
using System.Linq;
using Matikka.Core.Randomness;

namespace Matikka.Core.Games.PowerMerge
{
    /// <param name="Level">Taso.</param>
    /// <param name="Seed">Siemen: taso + siemen toistaa saman pulman.</param>
    /// <param name="Start">Aloituslauta.</param>
    /// <param name="Goals">Laatat, joiden pitää olla laudalla yhtä aikaa.</param>
    /// <param name="Par">Lyhimmän ratkaisun pituus (kolmen tähden raja).</param>
    /// <param name="NegativeNotation">Näytetäänkö jakajat negatiivisina eksponentteina (a⁻²) vai jakolaskuna (: a²).</param>
    public sealed record PowerPuzzle(int Level, long Seed, PowerBoard Start, IReadOnlyList<PowerGoal> Goals, int Par, bool NegativeNotation);

    /// <summary>
    /// Luo pulman: arpoo laudan tason laattavalikoimasta, käy leveyshaulla läpi kaikki tilat
    /// muutaman siirron syvyyteen ja valitsee tavoitteeksi laatan, joka syntyy aikaisintaan
    /// par-siirrolla. Näin par on aina tarkka ja tavoite vaatii vähintään kaksi siirtoa.
    /// </summary>
    public static class PowerPuzzleGenerator
    {
        public const int BoardSize = 4;
        private const int MaxAttempts = 600;

        private enum GoalPreference
        {
            Positive,
            One,
            Negative,
        }

        private sealed class LevelSpec
        {
            public string Bases = "a";
            public int PowersMin;
            public int PowersMax;
            public int ExpMax = 4;
            public int DivisorsMin;
            public int DivisorsMax;
            public int DivisorExpMax = 1;
            public int[] Raises = Array.Empty<int>();
            public int RaisesMin;
            public int RaisesMax;
            public int ParMin = 2;
            public int ParMax = 4;
            public GoalPreference Goal = GoalPreference.Positive;
            public int GoalCount = 1;
            public bool NegativeNotation;
            public int MaxGoalExponent = 24;
        }

        private static readonly LevelSpec[] Specs =
        {
            // 1 Sama kanta
            new LevelSpec { Bases = "a", PowersMin = 5, PowersMax = 6, ExpMax = 4, ParMin = 2, ParMax = 3 },

            // 2 Kaksi kantaa
            new LevelSpec { Bases = "ab", PowersMin = 7, PowersMax = 8, ExpMax = 4 },

            // 3 Jakolasku
            new LevelSpec { Bases = "ab", PowersMin = 5, PowersMax = 6, ExpMax = 5, DivisorsMin = 1, DivisorsMax = 2, DivisorExpMax = 3 },

            // 4 Nollas potenssi
            new LevelSpec { Bases = "ab", PowersMin = 5, PowersMax = 6, ExpMax = 4, DivisorsMin = 2, DivisorsMax = 3, DivisorExpMax = 4, Goal = GoalPreference.One },

            // 5 Potenssin potenssi
            new LevelSpec { Bases = "ab", PowersMin = 5, PowersMax = 6, ExpMax = 3, Raises = new[] { 2, 3 }, RaisesMin = 1, RaisesMax = 2, MaxGoalExponent = 36 },

            // 6 Negatiivinen eksponentti
            new LevelSpec
            {
                Bases = "ab", PowersMin = 4, PowersMax = 5, ExpMax = 4, DivisorsMin = 2, DivisorsMax = 3, DivisorExpMax = 5,
                Raises = new[] { 2 }, RaisesMin = 0, RaisesMax = 1, Goal = GoalPreference.Negative, NegativeNotation = true,
            },

            // 7 Mestari
            new LevelSpec
            {
                Bases = "ab", PowersMin = 6, PowersMax = 6, ExpMax = 4, DivisorsMin = 1, DivisorsMax = 2, DivisorExpMax = 4,
                Raises = new[] { 2, 3 }, RaisesMin = 1, RaisesMax = 1, ParMin = 3, ParMax = 5, GoalCount = 2,
                NegativeNotation = true, MaxGoalExponent = 30,
            },
        };

        public static int MaxLevel => Specs.Length;

        public static (int Min, int Max) ParRange(int level) => (Specs[level - 1].ParMin, Specs[level - 1].ParMax);

        public static PowerPuzzle Generate(int level, long seed)
        {
            if (level < 1 || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, "Tuntematon taso.");

            LevelSpec spec = Specs[level - 1];
            var random = new SeededRandom(seed);

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                PowerBoard board = RandomBoard(spec, random);
                if (TryPickGoals(board, spec, random, out IReadOnlyList<PowerGoal>? goals, out int par))
                    return new PowerPuzzle(level, seed, board, goals!, par, spec.NegativeNotation);
            }

            throw new InvalidOperationException($"Tason {level} pulmaa ei saatu luotua siemenellä {seed}.");
        }

        private static PowerBoard RandomBoard(LevelSpec spec, SeededRandom r)
        {
            var tiles = new List<PowerTile>();
            int id = 1;

            int powers = r.Next(spec.PowersMin, spec.PowersMax);
            for (int i = 0; i < powers; i++)
                tiles.Add(PowerTile.Power(id++, RandomBase(spec, r), r.Next(1, spec.ExpMax)));

            int divisors = r.Next(spec.DivisorsMin, spec.DivisorsMax);
            for (int i = 0; i < divisors; i++)
                tiles.Add(PowerTile.Power(id++, RandomBase(spec, r), -r.Next(1, spec.DivisorExpMax)));

            if (spec.Raises.Length > 0)
            {
                int raises = r.Next(spec.RaisesMin, spec.RaisesMax);
                for (int i = 0; i < raises; i++)
                    tiles.Add(PowerTile.Raise(id++, spec.Raises[r.Next(0, spec.Raises.Length - 1)]));
            }

            var positions = Enumerable.Range(0, BoardSize * BoardSize).ToList();
            ListShuffle.Shuffle(positions, r);

            var cells = new PowerTile?[BoardSize * BoardSize];
            for (int i = 0; i < tiles.Count; i++)
                cells[positions[i]] = tiles[i];

            return new PowerBoard(BoardSize, cells);
        }

        private static char RandomBase(LevelSpec spec, SeededRandom r) => spec.Bases[r.Next(0, spec.Bases.Length - 1)];

        private static bool TryPickGoals(PowerBoard start, LevelSpec spec, SeededRandom random,
            out IReadOnlyList<PowerGoal>? goals, out int par)
        {
            goals = null;
            par = 0;

            // Jokaisen ehdokkaan (laatan tai laattaparin) aikaisin syvyys, jolla se on laudalla.
            var singles = new Dictionary<string, (IReadOnlyList<PowerGoal> Goals, int Depth)>();
            var pairs = new Dictionary<string, (IReadOnlyList<PowerGoal> Goals, int Depth)>();

            var visited = new HashSet<string> { start.Key };
            var frontier = new List<PowerBoard> { start };
            Record(start, 0, spec, singles, pairs);

            for (int depth = 1; depth <= spec.ParMax; depth++)
            {
                var next = new List<PowerBoard>();
                foreach (PowerBoard board in frontier)
                {
                    foreach (Direction direction in PowerSolver.Directions)
                    {
                        SlideResult slide = board.Slide(direction);
                        if (!slide.Changed || !visited.Add(slide.Board.Key))
                            continue;

                        next.Add(slide.Board);
                        Record(slide.Board, depth, spec, singles, pairs);
                    }
                }

                frontier = next;
            }

            var source = spec.GoalCount == 2 ? pairs : singles;
            var candidates = source
                .Where(c => c.Value.Depth >= spec.ParMin && c.Value.Depth <= spec.ParMax)
                .Where(c => c.Value.Goals.All(g => singles[g.Key].Depth > 0)) // mikään tavoite ei ole valmiina laudalla
                .OrderBy(c => c.Key, StringComparer.Ordinal) // deterministinen järjestys kaikilla alustoilla
                .ToList();

            if (candidates.Count == 0)
                return false;

            // Ensin syvyys tasaisesti, sitten tavoite: muuten lyhyet pulmat olisivat yliedustettuina.
            var depths = candidates.Select(c => c.Value.Depth).Distinct().OrderBy(d => d).ToList();
            int chosenDepth = depths[random.Next(0, depths.Count - 1)];
            candidates = candidates.Where(c => c.Value.Depth == chosenDepth).ToList();

            var pick = candidates[random.Next(0, candidates.Count - 1)];
            goals = pick.Value.Goals;
            par = pick.Value.Depth;
            return true;
        }

        private static void Record(PowerBoard board, int depth, LevelSpec spec,
            Dictionary<string, (IReadOnlyList<PowerGoal> Goals, int Depth)> singles,
            Dictionary<string, (IReadOnlyList<PowerGoal> Goals, int Depth)> pairs)
        {
            var goalTiles = board.Tiles
                .Select(t => t.Tile)
                .Where(t => IsGoalCandidate(t, spec))
                .Select(PowerGoal.From)
                .ToList();

            foreach (PowerGoal goal in goalTiles)
            {
                if (!singles.ContainsKey(goal.Key))
                    singles[goal.Key] = (new[] { goal }, depth);
            }

            if (spec.GoalCount == 1)
                return;

            foreach (PowerGoal a in goalTiles.Where(g => g.Base == 'a'))
            {
                foreach (PowerGoal b in goalTiles.Where(g => g.Base == 'b'))
                {
                    string key = a.Key + "|" + b.Key;
                    if (!pairs.ContainsKey(key))
                        pairs[key] = (new[] { a, b }, depth);
                }
            }
        }

        private static bool IsGoalCandidate(PowerTile tile, LevelSpec spec)
        {
            if (spec.GoalCount == 2)
                return tile.Kind == TileKind.Power && Math.Abs(tile.Exponent) <= spec.MaxGoalExponent;

            switch (spec.Goal)
            {
                case GoalPreference.One:
                    return tile.Kind == TileKind.One;
                case GoalPreference.Negative:
                    return tile.Kind == TileKind.Power && tile.Exponent < 0 && -tile.Exponent <= spec.MaxGoalExponent;
                default:
                    return tile.Kind == TileKind.Power && tile.Exponent > 1 && tile.Exponent <= spec.MaxGoalExponent;
            }
        }
    }
}
