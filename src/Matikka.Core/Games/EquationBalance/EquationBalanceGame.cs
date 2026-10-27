using System.Collections.Generic;

namespace Matikka.Core.Games.EquationBalance
{
    /// <summary>
    /// Yhtälövaaka: ratkaise yhtälö tekemällä sama siirto vaa'an molemmille puolille.
    /// OPS 7.–9. lk: algebra, ensimmäisen asteen yhtälön ratkaiseminen.
    /// </summary>
    public static class EquationBalanceGame
    {
        public const string GameId = "yhtalovaaka";
        public const string SkillId = "algebra.yhtalot";

        /// <summary>
        /// Tasojen vaikeudet ovat Elo-asteikon alkuarvauksia. Kun dataa kertyy, palvelin voi
        /// kalibroida ne todellisista onnistumisprosenteista.
        /// </summary>
        public static readonly GameInfo Info = new GameInfo(
            GameId,
            SkillId,
            "Yhtälövaaka",
            new List<LevelInfo>
            {
                new LevelInfo(1, "Ensiaskel", "x + b = d, positiiviset luvut", -1.5),
                new LevelInfo(2, "Miinusmerkit", "x + b = d, myös negatiiviset luvut", -0.8),
                new LevelInfo(3, "Kerroin", "ax = d, jakaminen kertoimella", -0.3),
                new LevelInfo(4, "Kaksi askelta", "ax + b = d", 0.3),
                new LevelInfo(5, "Negatiivinen kerroin", "-ax + b = d", 0.8),
                new LevelInfo(6, "x molemmin puolin", "ax + b = cx + d, positiiviset luvut", 1.3),
                new LevelInfo(7, "Kaikki merkit", "ax + b = cx + d, myös negatiiviset", 1.8),
                new LevelInfo(8, "Mestari", "Isommat luvut ja kertoimet", 2.4),
            });
    }
}
