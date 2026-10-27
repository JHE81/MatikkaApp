using System;
using System.Collections.Generic;
using System.Globalization;

namespace Matikka.Core.Games.NumberHop
{
    /// <summary>
    /// Lukusuorahyppy: hahmo seisoo lukusuoralla ja sen pitää päästä tavoitelukuun. Jokainen
    /// laskukortti on kertakäyttöinen, ja korttien järjestys ratkaisee. Negatiivisella kertominen
    /// peilaa hahmon nollan yli, ja neliö vie aina positiiviselle puolelle.
    ///
    /// OPS 7. lk: negatiiviset luvut, laskutoimitukset niillä ja potenssin käsite.
    /// </summary>
    public static class NumberHopGame
    {
        public const string GameId = "lukusuorahyppy";
        public const string SkillId = "luvut.negatiiviset";

        public static readonly GameInfo Info = new GameInfo(
            GameId,
            SkillId,
            "Lukusuorahyppy",
            new List<LevelInfo>
            {
                new LevelInfo(1, "Nollan yli", "Lisää ja vähennä niin, että hahmo ylittää nollan", -1.5),
                new LevelInfo(2, "Miinus miinus", "Negatiivisen luvun lisääminen ja vähentäminen", -0.8),
                new LevelInfo(3, "Peilikertolasku", "Negatiivisella kertominen peilaa nollan yli", -0.1),
                new LevelInfo(4, "Jakolasku", "Jakaminen negatiivisella luvulla", 0.5),
                new LevelInfo(5, "Neliö ja kuutio", "(−3)² on positiivinen, (−3)³ negatiivinen", 1.1),
                new LevelInfo(6, "Mestari", "Kaikki laskutoimitukset sekaisin", 1.8),
            });
    }

    public enum CardOp
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Square,
        Cube,
    }

    public enum CardError
    {
        None,

        /// <summary>Jako ei mene tasan: peli pysyy kokonaisluvuissa.</summary>
        NotDivisible,

        /// <summary>Tulos menisi lukusuoran ulkopuolelle.</summary>
        OutOfRange,
    }

    /// <param name="Id">Kortin tunniste (kortti on kertakäyttöinen).</param>
    /// <param name="Op">Laskutoimitus.</param>
    /// <param name="Operand">Luku, jolla lasketaan (potensseilla ei käytössä).</param>
    public sealed record NumberCard(int Id, CardOp Op, int Operand = 0)
    {
        /// <summary>Lukusuoran ääripäät: luvut pysyvät pelattavina.</summary>
        public const int Range = 60;

        public CardError TryApply(int value, out int result)
        {
            long r;
            switch (Op)
            {
                case CardOp.Add: r = (long)value + Operand; break;
                case CardOp.Subtract: r = (long)value - Operand; break;
                case CardOp.Multiply: r = (long)value * Operand; break;
                case CardOp.Divide:
                    if (Operand == 0 || value % Operand != 0)
                    {
                        result = value;
                        return CardError.NotDivisible;
                    }

                    r = value / Operand;
                    break;
                case CardOp.Square: r = (long)value * value; break;
                default: r = (long)value * value * value; break;
            }

            if (Math.Abs(r) > Range)
            {
                result = value;
                return CardError.OutOfRange;
            }

            result = (int)r;
            return CardError.None;
        }

        /// <summary>Kortin teksti, esim. "+ (−4)", "· 3", "( )²". Miinusmerkkinä U+2212.</summary>
        public string Label
        {
            get
            {
                switch (Op)
                {
                    case CardOp.Add: return "+ " + Operand_();
                    case CardOp.Subtract: return "− " + Operand_();
                    case CardOp.Multiply: return "· " + Operand_();
                    case CardOp.Divide: return ": " + Operand_();
                    case CardOp.Square: return "( )²";
                    default: return "( )³";
                }
            }
        }

        /// <summary>Lasku auki kirjoitettuna, esim. "−3 · (−2) = 6" tai "(−4)² = 16".</summary>
        public string Describe(int before, int after)
        {
            string b = Num(before);
            switch (Op)
            {
                case CardOp.Square: return (before < 0 ? "(" + b + ")" : b) + "² = " + Num(after);
                case CardOp.Cube: return (before < 0 ? "(" + b + ")" : b) + "³ = " + Num(after);
                default: return b + " " + Label + " = " + Num(after);
            }
        }

        private string Operand_() => Operand < 0 ? "(" + Num(Operand) + ")" : Num(Operand);

        public static string Num(int n) =>
            n < 0 ? "−" + (-(long)n).ToString(CultureInfo.InvariantCulture) : n.ToString(CultureInfo.InvariantCulture);
    }

    /// <param name="Level">Taso.</param>
    /// <param name="Seed">Siemen.</param>
    /// <param name="Start">Lähtöluku.</param>
    /// <param name="Target">Tavoiteluku.</param>
    /// <param name="Hand">Kortit (kukin käytettävissä kerran).</param>
    /// <param name="Par">Vähimmäismäärä kortteja tavoitteeseen.</param>
    public sealed record NumberHopPuzzle(int Level, long Seed, int Start, int Target, IReadOnlyList<NumberCard> Hand, int Par);
}
