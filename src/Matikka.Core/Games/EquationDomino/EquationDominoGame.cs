using System.Collections.Generic;
using Matikka.Core.Games.EquationBalance;

namespace Matikka.Core.Games.EquationDomino
{
    /// <summary>
    /// Yhtälödomino: jokaisessa laatassa on vastaus (x = 4) ja yhtälö. Ketju jatkuu laatalla,
    /// jonka vastaus ratkaisee edellisen laatan yhtälön. Sama idea kuin luokkahuoneen
    /// "matikkadomino"-korteissa, mutta väärä valinta selitetään sijoittamalla.
    ///
    /// Jakaa taitoalueen Yhtälövaa'an kanssa: kumpikin peli kasvattaa samaa yhtälötaitoa.
    /// </summary>
    public static class EquationDominoGame
    {
        public const string GameId = "yhtalodomino";
        public const string SkillId = EquationBalanceGame.SkillId;

        public static readonly GameInfo Info = new GameInfo(
            GameId,
            SkillId,
            "Yhtälödomino",
            new List<LevelInfo>
            {
                new LevelInfo(1, "Lämmittely", "x + b = d, viisi laattaa", -1.5),
                new LevelInfo(2, "Kertoimet", "ax = d ja x + b = d, myös negatiiviset vastaukset", -0.6),
                new LevelInfo(3, "Kaksi askelta", "ax + b = d", 0.1),
                new LevelInfo(4, "Merkkiansat", "Vastauksissa sekä x = 3 että x = -3", 0.8),
                new LevelInfo(5, "Molemmin puolin", "ax + b = cx + d", 1.4),
                new LevelInfo(6, "Sulkeet", "a(x + b) = d ja a(x + b) = cx + d", 2.1),
            });
    }
}
