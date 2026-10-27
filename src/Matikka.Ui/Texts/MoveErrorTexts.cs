using Matikka.Core.Games.EquationBalance;

namespace Matikka.Ui.Texts;

/// <summary>Virheet pelaajan kielellä: mitä tapahtui ja mitä voi tehdä sen sijaan.</summary>
public static class MoveErrorTexts
{
    public static string For(MoveError error) => error switch
    {
        MoveError.NoEffect => "Tuo siirto ei muuta yhtälöä. Valitse toinen luku.",
        MoveError.MultiplyByZero => "Nollalla kertominen tekee molemmista puolista nollan. Kerro muulla luvulla.",
        MoveError.DivideByZero => "Nollalla ei voi jakaa.",
        MoveError.NotDivisible => "Kaikki luvut eivät jakaudu tasan. Kokeile ensin jotain muuta siirtoa.",
        MoveError.XTermNotAllowed => "x-termillä voi vain lisätä tai vähentää.",
        MoveError.NumbersTooLarge => "Luvut kasvaisivat liian suuriksi. Kumoa tai kokeile pienempää lukua.",
        _ => "Siirto ei onnistunut.",
    };
}
