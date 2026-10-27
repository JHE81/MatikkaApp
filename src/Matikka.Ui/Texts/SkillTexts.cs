namespace Matikka.Ui.Texts;

/// <summary>Taitoalueiden nimet pelaajalle. Uusi taitoalue lisätään tähän.</summary>
public static class SkillTexts
{
    public static string Title(string skillId) => skillId switch
    {
        "algebra.yhtalot" => "Yhtälöt",
        "algebra.potenssit" => "Potenssit",
        "ajattelu.algoritmit" => "Algoritminen ajattelu",
        "luvut.negatiiviset" => "Negatiiviset luvut",
        _ => skillId,
    };
}
