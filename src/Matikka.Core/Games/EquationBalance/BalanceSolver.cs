using System;
using System.Collections.Generic;

namespace Matikka.Core.Games.EquationBalance
{
    /// <summary>
    /// Ratkaisee yhtälön "oppikirjan järjestyksessä":
    /// 1) x-termit samalle puolelle, 2) vakio pois x:n puolelta, 3) jaetaan kertoimella.
    /// Käytetään par-arvoon (tähtien raja), vihjeisiin ja generaattorin tarkistukseen.
    /// </summary>
    public static class BalanceSolver
    {
        /// <summary>
        /// Palauttaa siirrot, joilla yhtälö ratkeaa. Heittää poikkeuksen, jos yhtälöllä ei ole
        /// yksikäsitteistä kokonaislukuratkaisua (generaattori ei tuota sellaisia).
        /// </summary>
        public static IReadOnlyList<BalanceMove> Solve(Equation start)
        {
            var moves = new List<BalanceMove>();
            Equation current = start;

            // Varmuusraja: kanoninen ratkaisu on aina korkeintaan kolme siirtoa.
            while (!current.IsSolved)
            {
                if (moves.Count >= 3)
                    throw new InvalidOperationException("Yhtälöä ei saatu ratkaistua: " + start);

                BalanceMove move = NextMoveOrThrow(current);
                MoveResult result = current.Apply(move);
                if (!result.Succeeded)
                    throw new InvalidOperationException("Ratkaisija tuotti virheellisen siirron " + move + " yhtälölle " + current);

                moves.Add(move);
                current = result.Equation!;
            }

            return moves;
        }

        /// <summary>Seuraava siirto kohti ratkaisua (vihje). Null, jos yhtälö on jo ratkaistu tai ratkeamaton.</summary>
        public static BalanceMove? NextMove(Equation current)
        {
            if (current.IsSolved)
                return null;

            try
            {
                return NextMoveOrThrow(current);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static BalanceMove NextMoveOrThrow(Equation eq)
        {
            LinearExpression left = eq.Left;
            LinearExpression right = eq.Right;

            if (left.X == right.X)
                throw new InvalidOperationException("Yhtälöllä ei ole yksikäsitteistä ratkaisua: " + eq);

            // 1) x-termit molemmilla puolilla: poistetaan pienempi kerroin, jolloin
            //    jäljelle jäävä kerroin on positiivinen.
            if (left.X != 0 && right.X != 0)
            {
                int remove = Math.Min(left.X, right.X);
                return remove > 0 ? BalanceMove.SubtractX(remove) : BalanceMove.AddX(-remove);
            }

            LinearExpression xSide = left.HasX ? left : right;

            // 2) Vakio pois x:n puolelta.
            if (xSide.Constant != 0)
            {
                return xSide.Constant > 0
                    ? BalanceMove.Subtract(xSide.Constant)
                    : BalanceMove.Add(-xSide.Constant);
            }

            // 3) Jaetaan x:n kertoimella.
            if (xSide.X != 1)
                return BalanceMove.DivideBy(xSide.X);

            throw new InvalidOperationException("Odottamaton tila: " + eq);
        }
    }
}
