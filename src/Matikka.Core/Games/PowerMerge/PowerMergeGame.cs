using System.Collections.Generic;

namespace Matikka.Core.Games.PowerMerge
{
    /// <summary>
    /// Potenssiyhdistely: 2048-tyylinen ruudukko, jossa laatat ovat potensseja. Kun samankantaiset
    /// laatat liukuvat yhteen, ne kerrotaan, eli eksponentit lasketaan yhteen. Jakajalaatat vähentävät
    /// eksponenttia ja (  )ⁿ-laatat kertovat sen. Tavoitteena on koota annettu potenssi.
    ///
    /// OPS 8. lk: potenssien laskusäännöt.
    /// </summary>
    public static class PowerMergeGame
    {
        public const string GameId = "potenssiyhdistely";
        public const string SkillId = "algebra.potenssit";

        public static readonly GameInfo Info = new GameInfo(
            GameId,
            SkillId,
            "Potenssiyhdistely",
            new List<LevelInfo>
            {
                new LevelInfo(1, "Sama kanta", "Kerro samankantaisia: eksponentit lasketaan yhteen", -1.4),
                new LevelInfo(2, "Kaksi kantaa", "Vain samankantaiset potenssit yhdistyvät", -0.8),
                new LevelInfo(3, "Jakolasku", "Jaa samankantaisia: eksponentit vähennetään", -0.2),
                new LevelInfo(4, "Nollas potenssi", "Kun eksponentiksi tulee nolla, potenssi on 1", 0.3),
                new LevelInfo(5, "Potenssin potenssi", "Korota potenssi potenssiin: eksponentit kerrotaan", 0.9),
                new LevelInfo(6, "Negatiivinen eksponentti", "Jakaja kirjoitetaan negatiivisena eksponenttina", 1.5),
                new LevelInfo(7, "Mestari", "Kaikki säännöt ja kaksi tavoitetta yhtä aikaa", 2.2),
            });
    }
}
