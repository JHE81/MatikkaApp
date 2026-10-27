using System;
using Matikka.Core.Randomness;

namespace Matikka.Core.Games.EquationBalance
{
    /// <param name="Level">Taso, jolta tehtävä on luotu.</param>
    /// <param name="Seed">Siemen, jolla tehtävä on luotu. Sama taso + siemen = sama tehtävä.</param>
    /// <param name="Start">Alkuyhtälö.</param>
    /// <param name="Solution">x:n arvo.</param>
    /// <param name="Par">Siirtojen määrä oppikirjaratkaisulla (kolmen tähden raja).</param>
    public sealed record BalancePuzzle(int Level, long Seed, Equation Start, int Solution, int Par);

    /// <summary>
    /// Luo tehtävät parametreista. Tehtäviä ei tallenneta valmiina, joten niitä riittää
    /// loputtomiin ja vaikeutta voi säätää yksittäisillä luvuilla.
    /// </summary>
    public static class BalancePuzzleGenerator
    {
        private const int MaxAttempts = 100;

        public static int MaxLevel => EquationBalanceGame.Info.Levels.Count;

        public static BalancePuzzle Generate(int level, long seed)
        {
            if (level < 1 || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, "Tuntematon taso.");

            var random = new SeededRandom(seed);

            // Satunnaisesti arvottu yhtälö voi joskus olla kelvoton (esim. valmiiksi ratkaistu).
            // Arvotaan uudestaan samasta siemenvirrasta, jolloin tulos on silti deterministinen.
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                int solution;
                Equation candidate = Build(level, random, out solution);

                if (IsAcceptable(candidate, solution))
                {
                    int par = BalanceSolver.Solve(candidate).Count;
                    return new BalancePuzzle(level, seed, candidate, solution, par);
                }
            }

            throw new InvalidOperationException($"Tason {level} tehtävää ei saatu luotua siemenellä {seed}.");
        }

        private static Equation Build(int level, SeededRandom r, out int x0)
        {
            int a, b, c, d;

            switch (level)
            {
                case 1: // x + b = d
                    x0 = r.Next(1, 9);
                    b = r.Next(1, 9);
                    return Equation.Of(1, b, 0, x0 + b);

                case 2: // x + b = d, negatiivisia lukuja
                    x0 = r.NextNonZero(1, 9);
                    b = r.NextNonZero(1, 12);
                    return Equation.Of(1, b, 0, x0 + b);

                case 3: // ax = d
                    a = r.Next(2, 9);
                    x0 = r.NextNonZero(1, 9);
                    return MaybeSwap(Equation.Of(a, 0, 0, a * x0), r);

                case 4: // ax + b = d
                    a = r.Next(2, 6);
                    b = r.NextNonZero(1, 12);
                    x0 = r.Next(1, 9);
                    return MaybeSwap(Equation.Of(a, b, 0, a * x0 + b), r);

                case 5: // -ax + b = d
                    a = -r.Next(1, 8);
                    b = r.NextNonZero(1, 15);
                    x0 = r.NextNonZero(1, 9);
                    return MaybeSwap(Equation.Of(a, b, 0, a * x0 + b), r);

                case 6: // ax + b = cx + d, a > c > 0
                    c = r.Next(1, 5);
                    a = c + r.Next(1, 5);
                    b = r.Next(0, 12);
                    x0 = r.Next(1, 9);
                    d = (a - c) * x0 + b;
                    return MaybeSwap(Equation.Of(a, b, c, d), r);

                case 7: // ax + b = cx + d, kaikki merkit
                    a = r.NextNonZero(1, 9);
                    c = r.NextNonZero(1, 9);
                    b = r.NextNonZero(1, 15);
                    x0 = r.NextNonZero(1, 12);
                    d = (a - c) * x0 + b;
                    return Equation.Of(a, b, c, d);

                default: // 8: isot luvut
                    a = r.NextNonZero(2, 12);
                    c = r.NextNonZero(1, 12);
                    b = r.NextNonZero(1, 40);
                    x0 = r.NextNonZero(1, 15);
                    d = (a - c) * x0 + b;
                    return MaybeSwap(Equation.Of(a, b, c, d), r);
            }
        }

        private static Equation MaybeSwap(Equation equation, SeededRandom r) =>
            r.Next(0, 3) == 0 ? equation.Swap() : equation;

        private static bool IsAcceptable(Equation equation, int solution)
        {
            if (equation.IsSolved)
                return false;
            if (equation.Left.X == equation.Right.X)
                return false; // ei yksikäsitteistä ratkaisua
            if (!equation.IsSatisfiedBy(solution))
                return false;

            // Pidetään luvut pelattavan kokoisina.
            const int limit = 200;
            return Math.Abs(equation.Left.Constant) <= limit && Math.Abs(equation.Right.Constant) <= limit;
        }
    }
}
