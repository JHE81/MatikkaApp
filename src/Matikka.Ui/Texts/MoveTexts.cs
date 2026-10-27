using System.Globalization;
using Matikka.Core.Games.EquationBalance;

namespace Matikka.Ui.Texts;

/// <summary>Siirto sanallisena ohjeena, esim. "jaa molemmat puolet luvulla 5".</summary>
public static class MoveTexts
{
    public static string Describe(BalanceMove move)
    {
        string number = move.Value.ToString(CultureInfo.InvariantCulture);
        string term = move.IsXTerm ? (move.Value == 1 ? "x" : number + "x") : number;

        return move.Operation switch
        {
            Operation.Add => $"lisää molemmille puolille {term}",
            Operation.Subtract => $"vähennä molemmilta puolilta {term}",
            Operation.Multiply => $"kerro molemmat puolet luvulla {number}",
            _ => $"jaa molemmat puolet luvulla {number}",
        };
    }
}
