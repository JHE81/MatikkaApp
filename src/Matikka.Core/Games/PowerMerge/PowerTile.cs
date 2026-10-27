using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Matikka.Core.Games.PowerMerge
{
    public enum TileKind
    {
        /// <summary>Potenssi, esim. a³. Negatiivinen eksponentti on jakaja: a⁻² = 1 : a².</summary>
        Power,

        /// <summary>Luku 1, esim. a⁰. Yhdistyy kaikkeen muuttamatta sitä.</summary>
        One,

        /// <summary>Korotuslaatta (  )ⁿ: korottaa potenssin potenssiin n, eli kertoo eksponentin.</summary>
        Raise,
    }

    public enum Direction
    {
        Left,
        Right,
        Up,
        Down,
    }

    public enum MergeRule
    {
        /// <summary>aᵐ · aⁿ = aᵐ⁺ⁿ (myös jakolasku, kun toinen eksponentti on negatiivinen).</summary>
        Product,

        /// <summary>(aᵐ)ⁿ = aᵐⁿ.</summary>
        PowerOfPower,

        /// <summary>Kaksi korotuslaattaa: ((  )ᵐ)ⁿ = (  )ᵐⁿ.</summary>
        ComposeRaises,

        /// <summary>1 · x = x ja 1ⁿ = 1.</summary>
        Identity,
    }

    /// <param name="Id">Pysyvä tunniste käyttöliittymän animaatioita varten. Ei vaikuta pelitilaan.</param>
    /// <param name="Kind">Laatan laji.</param>
    /// <param name="Base">Kanta (Power-laatoilla), muuten välilyönti.</param>
    /// <param name="Exponent">Eksponentti (Power) tai korotus n (Raise). One-laatalla 0.</param>
    public sealed record PowerTile(int Id, TileKind Kind, char Base, int Exponent)
    {
        /// <summary>Potenssi; nollaeksponentista tulee luku 1.</summary>
        public static PowerTile Power(int id, char @base, int exponent) =>
            exponent == 0 ? One(id) : new PowerTile(id, TileKind.Power, @base, exponent);

        public static PowerTile One(int id) => new PowerTile(id, TileKind.One, ' ', 0);

        public static PowerTile Raise(int id, int n) => new PowerTile(id, TileKind.Raise, ' ', n);

        /// <summary>Tunnisteeton avain, esim. "a5", "a-2", "1", "^3".</summary>
        public string Key =>
            Kind == TileKind.Power ? Base + Exponent.ToString(CultureInfo.InvariantCulture)
            : Kind == TileKind.One ? "1"
            : "^" + Exponent.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Tavoitelaatta. Vertaa laatan arvoa, ei tunnistetta.</summary>
    public sealed record PowerGoal(TileKind Kind, char Base, int Exponent)
    {
        public static readonly PowerGoal OneGoal = new PowerGoal(TileKind.One, ' ', 0);

        public static PowerGoal Of(char @base, int exponent) =>
            exponent == 0 ? OneGoal : new PowerGoal(TileKind.Power, @base, exponent);

        public static PowerGoal From(PowerTile tile) => new PowerGoal(tile.Kind, tile.Base, tile.Exponent);

        public bool Matches(PowerTile tile) => tile.Kind == Kind && tile.Base == Base && tile.Exponent == Exponent;

        public PowerTile ToTile(int id = 0) => new PowerTile(id, Kind, Base, Exponent);

        public string Key => ToTile().Key;
    }

    /// <summary>Yksi yhdistyminen siirron aikana: laatat, tulos ja käytetty sääntö.</summary>
    /// <param name="First">Seinää lähempänä ollut laatta.</param>
    /// <param name="Second">Sen perään liukunut laatta.</param>
    public sealed record MergeInfo(PowerTile First, PowerTile Second, PowerTile Result, MergeRule Rule);

    public static class PowerRules
    {
        /// <summary>Eksponentit pidetään pelattavan kokoisina; suurempaa tulosta ei synny.</summary>
        public const int MaxExponent = 99;

        /// <summary>
        /// Yhdistääkö kaksi vierekkäistä laattaa, ja mikä tulos on. Tulos saa jälkimmäisen
        /// laatan tunnisteen, jolloin käyttöliittymä näyttää sen liukuvan edellisen päälle.
        /// </summary>
        public static bool TryCombine(PowerTile first, PowerTile second, [NotNullWhen(true)] out PowerTile? result, out MergeRule rule)
        {
            int id = second.Id;
            result = null;
            rule = MergeRule.Product;

            if (first.Kind == TileKind.One || second.Kind == TileKind.One)
            {
                PowerTile other = first.Kind == TileKind.One ? second : first;
                rule = MergeRule.Identity;
                result = other.Kind == TileKind.Raise ? PowerTile.One(id) : other with { Id = id };
                return true;
            }

            if (first.Kind == TileKind.Raise && second.Kind == TileKind.Raise)
            {
                long n = (long)first.Exponent * second.Exponent;
                if (n > MaxExponent)
                    return false;

                rule = MergeRule.ComposeRaises;
                result = PowerTile.Raise(id, (int)n);
                return true;
            }

            if (first.Kind == TileKind.Raise || second.Kind == TileKind.Raise)
            {
                PowerTile raise = first.Kind == TileKind.Raise ? first : second;
                PowerTile power = first.Kind == TileKind.Raise ? second : first;
                long e = (long)power.Exponent * raise.Exponent;
                if (Math.Abs(e) > MaxExponent)
                    return false;

                rule = MergeRule.PowerOfPower;
                result = PowerTile.Power(id, power.Base, (int)e);
                return true;
            }

            // Kaksi potenssia: vain sama kanta yhdistyy.
            if (first.Base != second.Base)
                return false;

            long sum = (long)first.Exponent + second.Exponent;
            if (Math.Abs(sum) > MaxExponent)
                return false;

            rule = MergeRule.Product;
            result = PowerTile.Power(id, first.Base, (int)sum);
            return true;
        }
    }
}
