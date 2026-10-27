using System;
using System.Collections.Generic;
using Matikka.Core.Games.EquationBalance;

namespace Matikka.Core.Games.EquationDomino
{
    /// <summary>
    /// Laatan yhtälö. <see cref="Display"/> on se, mitä pelaaja näkee (esim. sulkeilla),
    /// <see cref="Expanded"/> sama yhtälö auki kirjoitettuna sijoitustarkistusta ja vihjeitä varten.
    /// </summary>
    /// <param name="Expanded">Yhtälö muodossa ax + b = cx + d.</param>
    /// <param name="Display">Näytettävä muoto.</param>
    /// <param name="Solution">x:n arvo.</param>
    /// <param name="BracketFactor">Sulkeiden edessä oleva kerroin, jos yhtälö on muotoa a(x + b) = d.</param>
    public sealed record DominoEquation(Equation Expanded, string Display, int Solution, int? BracketFactor = null)
    {
        public long LeftAt(int x) => (long)Expanded.Left.X * x + Expanded.Left.Constant;

        public long RightAt(int x) => (long)Expanded.Right.X * x + Expanded.Right.Constant;

        /// <summary>
        /// Ensimmäinen järkevä ratkaisuaskel. Sulkeyhtälössä a(x + b) = d jaetaan ensin a:lla,
        /// jolloin sulkeita ei tarvitse avata.
        /// </summary>
        public BalanceMove? FirstStep =>
            BracketFactor is int a ? BalanceMove.DivideBy(a) : BalanceSolver.NextMove(Expanded);

        public override string ToString() => Display;
    }

    /// <summary>
    /// Dominolaatta: vasemmalla vastaus, oikealla yhtälö. Aloituslaatalla ei ole vastausta,
    /// viimeisellä laatalla ei ole yhtälöä (ketju päättyy maaliin).
    /// </summary>
    public sealed record DominoTile(int Id, int? Answer, DominoEquation? Equation)
    {
        public bool IsStart => Answer == null;

        public bool IsFinish => Equation == null;
    }

    /// <param name="Level">Taso.</param>
    /// <param name="Seed">Siemen: taso + siemen toistaa saman pelin.</param>
    /// <param name="Start">Aloituslaatta, valmiiksi ketjussa.</param>
    /// <param name="Hand">Pelaajan laatat sekoitetussa järjestyksessä.</param>
    public sealed record DominoPuzzle(int Level, long Seed, DominoTile Start, IReadOnlyList<DominoTile> Hand);

    public readonly struct PlaceResult
    {
        public PlaceResult(bool correct, int triedAnswer, long leftValue, long rightValue)
        {
            Correct = correct;
            TriedAnswer = triedAnswer;
            LeftValue = leftValue;
            RightValue = rightValue;
        }

        public bool Correct { get; }

        /// <summary>Kokeiltu vastaus (väärässä valinnassa palautetta varten).</summary>
        public int TriedAnswer { get; }

        /// <summary>Avoimen yhtälön vasemman puolen arvo, kun x = kokeiltu vastaus.</summary>
        public long LeftValue { get; }

        public long RightValue { get; }
    }
}
