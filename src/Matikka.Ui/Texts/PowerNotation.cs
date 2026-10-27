using System.Globalization;
using Matikka.Core.Games.PowerMerge;
using Microsoft.AspNetCore.Components;

namespace Matikka.Ui.Texts;

/// <summary>
/// Potenssit HTML:nä (yläindeksit &lt;sup&gt;-elementteinä). Unicoden yläindeksimerkkejä ei käytetä,
/// koska sovelluksen fontissa ei ole niitä kaikkia. Sisältö syntyy vain luvuista ja kannoista,
/// joten MarkupString on turvallinen.
/// </summary>
public static class PowerNotation
{
    private const string Minus = "−";
    private const string Dot = " · ";
    private const string Slot = "<span class=\"slot\"></span>";

    public static MarkupString Tile(PowerTile tile, bool negativeNotation) => new(TileHtml(tile, negativeNotation));

    public static MarkupString Goal(PowerGoal goal, bool negativeNotation) => new(TileHtml(goal.ToTile(), negativeNotation));

    /// <summary>Laatan teksti: a³, a, 1, (  )², jakolaskumerkinnässä ": a²", muuten a⁻².</summary>
    public static string TileHtml(PowerTile tile, bool negativeNotation) => tile.Kind switch
    {
        TileKind.One => "1",
        TileKind.Raise => $"({Slot})<sup>{Num(tile.Exponent)}</sup>",
        _ when tile.Exponent < 0 && !negativeNotation => ":&#8202;" + Pow(tile.Base, -tile.Exponent),
        _ => Pow(tile.Base, tile.Exponent),
    };

    /// <summary>
    /// Yhdistyminen laskusääntönä auki kirjoitettuna, esim. a² · a³ = a²⁺³ = a⁵.
    /// Pelaaja näkee joka siirrolla, mikä sääntö tuotti tuloksen.
    /// </summary>
    public static MarkupString Formula(MergeInfo merge, bool negativeNotation) => new(FormulaHtml(merge, negativeNotation));

    public static string FormulaHtml(MergeInfo m, bool neg)
    {
        switch (m.Rule)
        {
            case MergeRule.Identity:
            {
                PowerTile other = m.First.Kind == TileKind.One ? m.Second : m.First;
                if (other.Kind == TileKind.One)
                    return "1" + Dot + "1 = 1";
                if (other.Kind == TileKind.Raise)
                    return $"1<sup>{Num(other.Exponent)}</sup> = 1";
                string t = TileHtml(other, true);
                return other.Exponent < 0 && !neg
                    ? $"1{Dot}(1 : {Pow(other.Base, -other.Exponent)}) = 1 : {Pow(other.Base, -other.Exponent)}"
                    : $"1{Dot}{t} = {t}";
            }

            case MergeRule.ComposeRaises:
                return $"(({Slot})<sup>{Num(m.First.Exponent)}</sup>)<sup>{Num(m.Second.Exponent)}</sup> = "
                       + $"({Slot})<sup>{Num(m.First.Exponent)}·{Num(m.Second.Exponent)}</sup> = ({Slot})<sup>{Num(m.Result.Exponent)}</sup>";

            case MergeRule.PowerOfPower:
            {
                PowerTile raise = m.First.Kind == TileKind.Raise ? m.First : m.Second;
                PowerTile power = m.First.Kind == TileKind.Raise ? m.Second : m.First;
                char b = power.Base;
                int e = power.Exponent;
                int n = raise.Exponent;

                if (e < 0 && !neg)
                    return $"(1 : {Pow(b, -e)})<sup>{n}</sup> = 1 : {b}<sup>{Num(-e)}·{n}</sup> = 1 : {Pow(b, -e * n)}";

                string inner = e == 1 ? b.ToString() : $"{b}<sup>{Num(e)}</sup>";
                return $"({inner})<sup>{n}</sup> = {b}<sup>{Num(e)}·{n}</sup> = {Pow(b, e * n)}";
            }

            default:
                return Product(m, neg);
        }
    }

    private static string Product(MergeInfo m, bool neg)
    {
        char b = m.First.Base;
        int e1 = m.First.Exponent;
        int e2 = m.Second.Exponent;
        int r = e1 + e2;
        string zero = r == 0 ? $" = {b}<sup>0</sup> = 1" : "";

        if (neg || (e1 > 0 && e2 > 0))
        {
            string sum = e2 < 0 ? $"{Num(e1)}+({Num(e2)})" : $"{Num(e1)}+{Num(e2)}";
            return $"{Pow(b, e1)}{Dot}{Pow(b, e2)} = {b}<sup>{sum}</sup>" + (r == 0 ? zero : $" = {Pow(b, r)}");
        }

        // Jakolaskumerkintä: kirjoitetaan jaettava ensin ja jakaja kaksoispisteen jälkeen.
        if (e1 < 0 && e2 < 0)
            return $"1 : {Pow(b, -e1)} : {Pow(b, -e2)} = 1 : {b}<sup>{Num(-e1)}+{Num(-e2)}</sup> = 1 : {Pow(b, -r)}";

        int p = e1 > 0 ? e1 : e2;
        int k = e1 > 0 ? -e2 : -e1;
        string head = $"{Pow(b, p)} : {Pow(b, k)} = {b}<sup>{Num(p)}{Minus}{Num(k)}</sup>";
        if (r == 0)
            return head + zero;
        return r > 0 ? $"{head} = {Pow(b, r)}" : $"{head} = 1 : {Pow(b, -r)}";
    }

    private static string Pow(char b, int e) => e == 1 ? b.ToString() : $"{b}<sup>{Num(e)}</sup>";

    private static string Num(int n) =>
        n < 0 ? Minus + (-n).ToString(CultureInfo.InvariantCulture) : n.ToString(CultureInfo.InvariantCulture);
}
