using System;
using System.Collections.Generic;
using System.Linq;
using Matikka.Core.Games.EquationBalance;
using Matikka.Core.Randomness;

namespace Matikka.Core.Games.EquationDomino
{
    /// <summary>
    /// Luo dominopelin: ensin valitaan erilliset vastaukset, sitten jokaiselle yhtälö, jonka
    /// ratkaisu on seuraavan laatan vastaus. Koska vastaukset ovat erillisiä, jokaisessa vaiheessa
    /// täsmälleen yksi laatta sopii ketjuun.
    /// </summary>
    public static class DominoGenerator
    {
        private const int MaxAttempts = 200;
        private const int ConstantLimit = 200;

        public static int MaxLevel => EquationDominoGame.Info.Levels.Count;

        public static DominoPuzzle Generate(int level, long seed)
        {
            if (level < 1 || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, "Tuntematon taso.");

            var random = new SeededRandom(seed);
            List<int> answers = ChooseAnswers(level, random);
            int count = answers.Count;

            // Yhtälö i (aloituslaatasta alkaen) ratkeaa vastauksella answers[i].
            var equations = new List<DominoEquation>(count);
            foreach (int answer in answers)
                equations.Add(BuildEquation(level, answer, random));

            var start = new DominoTile(0, null, equations[0]);
            var chain = new List<DominoTile>(count);
            for (int i = 0; i < count; i++)
            {
                DominoEquation? next = i + 1 < count ? equations[i + 1] : null;
                chain.Add(new DominoTile(i + 1, answers[i], next));
            }

            // Sekoitetaan niin, ettei käsi ole valmiiksi oikeassa järjestyksessä.
            var hand = chain.ToList();
            do
            {
                ListShuffle.Shuffle(hand, random);
            }
            while (hand.Select(t => t.Id).SequenceEqual(chain.Select(t => t.Id)));

            return new DominoPuzzle(level, seed, start, hand);
        }

        private static List<int> ChooseAnswers(int level, SeededRandom r)
        {
            switch (level)
            {
                case 1:
                    return Distinct(r, 5, () => r.Next(1, 9));

                case 2:
                case 3:
                    return Distinct(r, 6, () => r.NextNonZero(1, 9));

                case 4:
                {
                    // Merkkiansat: kolme lukuparia x = a ja x = -a sekä yksi lisäluku.
                    List<int> magnitudes = Distinct(r, 4, () => r.Next(1, 9));
                    var answers = new List<int>();
                    for (int i = 0; i < 3; i++)
                    {
                        answers.Add(magnitudes[i]);
                        answers.Add(-magnitudes[i]);
                    }

                    answers.Add(r.NextBool() ? magnitudes[3] : -magnitudes[3]);
                    ListShuffle.Shuffle(answers, r);
                    return answers;
                }

                case 5:
                    return Distinct(r, 7, () => r.NextNonZero(1, 10));

                default:
                    return Distinct(r, 8, () => r.NextNonZero(1, 9));
            }
        }

        private static List<int> Distinct(SeededRandom r, int count, Func<int> next)
        {
            var result = new List<int>(count);
            while (result.Count < count)
            {
                int value = next();
                if (!result.Contains(value))
                    result.Add(value);
            }

            return result;
        }

        private static DominoEquation BuildEquation(int level, int x0, SeededRandom r)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                DominoEquation candidate = Candidate(level, x0, r);
                if (IsAcceptable(candidate))
                    return candidate;
            }

            throw new InvalidOperationException($"Tason {level} yhtälöä ei saatu luotua ratkaisulle {x0}.");
        }

        private static DominoEquation Candidate(int level, int x0, SeededRandom r)
        {
            int a, b, c, d;
            switch (level)
            {
                case 1: // x + b = d
                    b = r.Next(1, 9);
                    return Plain(Equation.Of(1, b, 0, x0 + b), x0);

                case 2: // x + b = d tai ax = d
                    if (r.NextBool())
                    {
                        b = r.NextNonZero(1, 12);
                        return Plain(MaybeSwap(Equation.Of(1, b, 0, x0 + b), r), x0);
                    }

                    a = r.Next(2, 9);
                    return Plain(MaybeSwap(Equation.Of(a, 0, 0, a * x0), r), x0);

                case 3: // ax + b = d
                    a = r.Next(2, 6);
                    b = r.NextNonZero(1, 12);
                    return Plain(MaybeSwap(Equation.Of(a, b, 0, a * x0 + b), r), x0);

                case 4: // negatiivinen kerroin tai vakio
                    a = r.NextBool() ? -r.Next(1, 6) : r.Next(2, 6);
                    b = a < 0 ? r.NextNonZero(1, 12) : -r.Next(1, 12);
                    return Plain(Equation.Of(a, b, 0, a * x0 + b), x0);

                case 5: // ax + b = cx + d
                    a = r.NextNonZero(1, 7);
                    c = r.NextNonZero(1, 7);
                    b = r.NextNonZero(1, 12);
                    d = (a - c) * x0 + b;
                    return Plain(Equation.Of(a, b, c, d), x0);

                default: // a(x + b) = d tai a(x + b) = cx + d
                    a = r.Next(2, 5);
                    b = r.NextNonZero(1, 6);
                    if (r.NextBool())
                    {
                        d = a * (x0 + b);
                        return new DominoEquation(Equation.Of(a, a * b, 0, d), Bracket(a, b) + " = " + d, x0, a);
                    }

                    c = r.NextNonZero(1, 4);
                    d = a * (x0 + b) - c * x0;
                    var right = new LinearExpression(c, d);
                    return new DominoEquation(Equation.Of(a, a * b, c, d), Bracket(a, b) + " = " + right, x0);
            }
        }

        private static DominoEquation Plain(Equation equation, int x0) =>
            new DominoEquation(equation, equation.ToString(), x0);

        private static Equation MaybeSwap(Equation equation, SeededRandom r) =>
            r.Next(0, 3) == 0 ? equation.Swap() : equation;

        private static string Bracket(int a, int b) =>
            a + "(x " + (b > 0 ? "+ " : "- ") + Math.Abs(b) + ")";

        private static bool IsAcceptable(DominoEquation e)
        {
            Equation eq = e.Expanded;
            return !eq.IsSolved
                && eq.Left.X != eq.Right.X
                && eq.IsSatisfiedBy(e.Solution)
                && Math.Abs(eq.Left.Constant) <= ConstantLimit
                && Math.Abs(eq.Right.Constant) <= ConstantLimit;
        }
    }
}
