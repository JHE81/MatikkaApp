using System.Collections.Generic;
using System.Linq;
using Matikka.Core.Games.CodePath;
using Matikka.Core.Games.EquationBalance;
using Matikka.Core.Games.EquationDomino;
using Matikka.Core.Games.NumberHop;
using Matikka.Core.Games.PowerMerge;

namespace Matikka.Core.Games
{
    /// <summary>Kaikki sovelluksen pelit. Uusi peli lisätään tähän listaan.</summary>
    public static class GameCatalog
    {
        public static readonly IReadOnlyList<GameInfo> All = new List<GameInfo>
        {
            EquationBalanceGame.Info,
            EquationDominoGame.Info,
            NumberHopGame.Info,
            PowerMergeGame.Info,
            CodePathGame.Info,
        };

        /// <summary>Null, jos peliä ei tunneta (esim. uudemman sovellusversion tapahtuma toiselta laitteelta).</summary>
        public static GameInfo? Find(string gameId) => All.FirstOrDefault(g => g.GameId == gameId);
    }
}
