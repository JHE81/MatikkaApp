using System;
using System.Collections.Generic;
using System.Linq;
using Matikka.Core.Games.EquationBalance;
using Matikka.Core.Progress;

namespace Matikka.Core.Games.EquationDomino
{
    /// <summary>Yksi dominopeli: ketju, käsi, virheet ja vihjeet.</summary>
    public sealed class DominoSession
    {
        private readonly List<DominoTile> _chain = new List<DominoTile>();
        private readonly List<DominoTile> _hand;

        public DominoSession(DominoPuzzle puzzle)
        {
            Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
            _chain.Add(puzzle.Start);
            _hand = puzzle.Hand.ToList();
        }

        public DominoPuzzle Puzzle { get; }

        public IReadOnlyList<DominoTile> Chain => _chain;

        public IReadOnlyList<DominoTile> Hand => _hand;

        /// <summary>Yhtälö, johon seuraavan laatan vastauksen pitää sopia. Null, kun ketju on valmis.</summary>
        public DominoEquation? OpenEquation => _chain[_chain.Count - 1].Equation;

        public int TileCount => Puzzle.Hand.Count;

        public int Placed => _chain.Count - 1;

        public int Mistakes { get; private set; }

        public int HintsUsed { get; private set; }

        public bool IsSolved => _hand.Count == 0;

        public int Stars => IsSolved ? CalculateStars(Mistakes, HintsUsed) : 0;

        /// <summary>
        /// Yrittää liittää laatan ketjuun. Väärästä valinnasta palautetaan sijoituksen tulos
        /// (vasen ja oikea puoli), jotta pelaaja näkee, miksi vastaus ei käy.
        /// </summary>
        public PlaceResult TryPlace(int tileId)
        {
            DominoEquation open = OpenEquation ?? throw new InvalidOperationException("Ketju on jo valmis.");
            DominoTile tile = _hand.FirstOrDefault(t => t.Id == tileId)
                ?? throw new ArgumentException("Laatta ei ole kädessä.", nameof(tileId));

            int answer = tile.Answer!.Value;
            var result = new PlaceResult(answer == open.Solution, answer, open.LeftAt(answer), open.RightAt(answer));

            if (result.Correct)
            {
                _hand.Remove(tile);
                _chain.Add(tile);
            }
            else
            {
                Mistakes++;
            }

            return result;
        }

        /// <summary>Ratkaisun ensimmäinen askel avoimelle yhtälölle. Vihje rajoittaa tähdet kahteen.</summary>
        public BalanceMove? TakeHint()
        {
            BalanceMove? hint = OpenEquation?.FirstStep;
            if (hint != null)
                HintsUsed++;
            return hint;
        }

        /// <summary>Virheettä 3 tähteä, enintään kaksi virhettä 2 tähteä, muuten 1. Vihjeellä enintään 2.</summary>
        public static int CalculateStars(int mistakes, int hintsUsed)
        {
            int stars = mistakes == 0 ? 3 : mistakes <= 2 ? 2 : 1;
            return hintsUsed > 0 ? Math.Min(stars, 2) : stars;
        }

        public GameEvent ToEvent(GameOutcome outcome, Guid id, long durationMs, DateTimeOffset occurredAt)
        {
            if (outcome == GameOutcome.Solved && !IsSolved)
                throw new InvalidOperationException("Keskeneräistä peliä ei voi tallentaa ratkaistuksi.");

            return new GameEvent
            {
                Id = id,
                GameId = EquationDominoGame.GameId,
                SkillId = EquationDominoGame.SkillId,
                Level = Puzzle.Level,
                Seed = Puzzle.Seed,
                Outcome = outcome,
                Moves = Placed + Mistakes,
                Par = TileCount,
                HintsUsed = HintsUsed,
                Stars = outcome == GameOutcome.Solved ? Stars : 0,
                DurationMs = durationMs,
                OccurredAt = occurredAt,
            };
        }
    }
}
