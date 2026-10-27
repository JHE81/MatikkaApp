using System;
using System.Collections.Generic;
using System.Linq;
using Matikka.Core.Randomness;

namespace Matikka.Core.Games.NumberHop
{
    /// <summary>
    /// Luo pulman eteenpäin: arpoo ratkaisukortit ja kulkee ne lähtöluvusta, jolloin tavoite on
    /// varmasti saavutettavissa. Lisäksi käteen tulee harhautuskortteja. Lopuksi leveyshaku
    /// laskee tarkan par-arvon ja tarkistaa, että ratkaisu harjoittaa tason sisältöä.
    /// </summary>
    public static class NumberHopGenerator
    {
        private const int MaxAttempts = 3000;

        public static int MaxLevel => NumberHopGame.Info.Levels.Count;

        public static (int Min, int Max) ParRange(int level) => level <= 2 ? (2, 3) : (2, 4);

        public static NumberHopPuzzle Generate(int level, long seed)
        {
            if (level < 1 || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, "Tuntematon taso.");

            var r = new SeededRandom(seed);
            var (parMin, parMax) = ParRange(level);

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                int id = 1;
                int start = level == 5 ? r.NextNonZero(1, 6) : r.Next(-8, 8);
                int length = r.Next(parMin, parMax);

                // Ratkaisu: kortti kerrallaan, vain sallittuja siirtoja.
                var solution = new List<NumberCard>();
                int value = start;
                bool ok = true;
                for (int i = 0; i < length && ok; i++)
                {
                    NumberCard card = RandomCard(level, r, id++, value);
                    ok = card.TryApply(value, out value) == CardError.None;
                    solution.Add(card);
                }

                if (!ok || value == start || Math.Abs(value) > 40)
                    continue;

                int target = value;
                var hand = new List<NumberCard>(solution);
                int distractors = level == 1 ? 2 : 3;
                for (int i = 0; i < distractors; i++)
                    hand.Add(RandomCard(level, r, id++, r.Next(-10, 10)));

                // Ei kahta täsmälleen samaa korttia: ne näyttäisivät samalta mutta tunnisteet eroavat.
                if (hand.Select(c => (c.Op, c.Operand)).Distinct().Count() != hand.Count)
                    continue;

                ListShuffle.Shuffle(hand, r);

                IReadOnlyList<int>? best = NumberHopSolver.Solve(start, target, hand);
                if (best == null || best.Count < parMin || best.Count > parMax)
                    continue;

                if (!PracticesLevel(level, start, best.Select(cid => hand.First(c => c.Id == cid)).ToList()))
                    continue;

                return new NumberHopPuzzle(level, seed, start, target, hand, best.Count);
            }

            throw new InvalidOperationException($"Tason {level} pulmaa ei saatu luotua siemenellä {seed}.");
        }

        private static NumberCard RandomCard(int level, SeededRandom r, int id, int value)
        {
            switch (level)
            {
                case 1:
                    return r.NextBool() ? new NumberCard(id, CardOp.Add, r.Next(1, 9)) : new NumberCard(id, CardOp.Subtract, r.Next(2, 12));

                case 2:
                    switch (r.Next(0, 3))
                    {
                        case 0: return new NumberCard(id, CardOp.Add, -r.Next(1, 9));
                        case 1: return new NumberCard(id, CardOp.Subtract, -r.Next(1, 9));
                        case 2: return new NumberCard(id, CardOp.Add, r.Next(1, 9));
                        default: return new NumberCard(id, CardOp.Subtract, r.Next(1, 9));
                    }

                case 3:
                    return r.Next(0, 2) == 0
                        ? AddOrSubtract(r, id)
                        : new NumberCard(id, CardOp.Multiply, Pick(r, -1, -2, -3, 2, 3));

                case 4:
                {
                    int kind = r.Next(0, 2);
                    if (kind == 0)
                        return AddOrSubtract(r, id);
                    if (kind == 1)
                        return new NumberCard(id, CardOp.Multiply, Pick(r, -1, -2, 2, 3));

                    // Jakaja, joka menee nykyiseen arvoon tasan, jos sellainen on (muuten satunnainen).
                    int[] divisors = { 2, -2, 3, -3, 4, -4, 5, -5 };
                    var fitting = divisors.Where(d => value != 0 && value % d == 0).ToList();
                    return new NumberCard(id, CardOp.Divide, fitting.Count > 0 ? fitting[r.Next(0, fitting.Count - 1)] : Pick(r, divisors));
                }

                case 5:
                    switch (r.Next(0, 3))
                    {
                        case 0: return new NumberCard(id, CardOp.Square);
                        case 1: return new NumberCard(id, CardOp.Cube);
                        default: return AddOrSubtract(r, id);
                    }

                default:
                    switch (r.Next(0, 5))
                    {
                        case 0: return new NumberCard(id, CardOp.Square);
                        case 1: return new NumberCard(id, CardOp.Multiply, Pick(r, -1, -2, -3, 2));
                        case 2:
                        {
                            var fitting = new[] { 2, -2, 3, -3 }.Where(d => value != 0 && value % d == 0).ToList();
                            return new NumberCard(id, CardOp.Divide, fitting.Count > 0 ? fitting[r.Next(0, fitting.Count - 1)] : -2);
                        }
                        default: return AddOrSubtract(r, id);
                    }
            }
        }

        private static NumberCard AddOrSubtract(SeededRandom r, int id)
        {
            int n = r.NextNonZero(1, 9);
            return r.NextBool() ? new NumberCard(id, CardOp.Add, n) : new NumberCard(id, CardOp.Subtract, n);
        }

        private static int Pick(SeededRandom r, params int[] options) => options[r.Next(0, options.Length - 1)];

        /// <summary>Lyhimmän ratkaisun pitää harjoittaa tason asiaa (ei esim. ohittaa negatiivista kertolaskua).</summary>
        private static bool PracticesLevel(int level, int start, IReadOnlyList<NumberCard> best)
        {
            var values = new List<int> { start };
            foreach (NumberCard c in best)
            {
                c.TryApply(values[values.Count - 1], out int next);
                values.Add(next);
            }

            bool crossesZero = values.Any(v => v < 0) && values.Any(v => v > 0);
            int Before(int i) => values[i];

            switch (level)
            {
                case 1:
                    return crossesZero;
                case 2:
                    return best.Any(c => c.Operand < 0);
                case 3:
                    return best.Any(c => c.Op == CardOp.Multiply && c.Operand < 0);
                case 4:
                    return best.Any(c => c.Op == CardOp.Divide && c.Operand < 0);
                case 5:
                    return Enumerable.Range(0, best.Count).Any(i => (best[i].Op == CardOp.Square || best[i].Op == CardOp.Cube) && Before(i) < 0);
                default:
                    return best.Select(c => c.Op).Distinct().Count() >= 2 && crossesZero;
            }
        }
    }
}
