using System.Collections.Generic;

namespace Matikka.Core.Games.CodePath
{
    /// <summary>
    /// Koodipolku: pelaaja kokoaa hahmolle ohjelman lohkoista ja ajaa sen. Hahmon pitää päästä
    /// labyrintin portaaliin (ja myöhemmin kerätä kristallit) osumatta seiniin.
    ///
    /// OPS 7.–9. lk: algoritminen ajattelu – peräkkäisyys, toisto, aliohjelmat ja virheiden etsintä.
    /// </summary>
    public static class CodePathGame
    {
        public const string GameId = "koodipolku";
        public const string SkillId = "ajattelu.algoritmit";

        public static readonly GameInfo Info = new GameInfo(
            GameId,
            SkillId,
            "Koodipolku",
            new List<LevelInfo>
            {
                new LevelInfo(1, "Ensiaskeleet", "Eteen ja käännökset oikeassa järjestyksessä", -1.5),
                new LevelInfo(2, "Kristallit", "Kerää kaikki kristallit ennen portaalia", -0.8),
                new LevelInfo(3, "Toisto", "Pitkät suorat lyhyesti: Toista n kertaa", -0.1),
                new LevelInfo(4, "Portaat", "Toista kokonainen kuvio", 0.6),
                new LevelInfo(5, "Aliohjelma", "Kirjoita toistuva pätkä kerran ja kutsu sitä", 1.3),
                new LevelInfo(6, "Mestari", "Aliohjelma ja toisto yhdessä", 2.0),
            });
    }
}
