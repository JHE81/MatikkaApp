using System;
using System.Collections.Generic;
using System.Linq;
using Matikka.Core.Progress;

namespace Matikka.Core.Games.NumberHop
{
    /// <param name="Card">Pelattu kortti.</param>
    /// <param name="Before">Luku ennen korttia.</param>
    /// <param name="After">Luku kortin jälkeen.</param>
    public sealed record HopMove(NumberCard Card, int Before, int After);

    /// <summary>Yksi pulma: nykyinen luku, pelatut kortit, kumoaminen ja vihjeet.</summary>
    public sealed class NumberHopSession
    {
        private readonly List<HopMove> _moves = new List<HopMove>();

        public NumberHopSession(NumberHopPuzzle puzzle)
        {
            Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
        }

        public NumberHopPuzzle Puzzle { get; }

        public int Current => _moves.Count == 0 ? Puzzle.Start : _moves[_moves.Count - 1].After;

        public IReadOnlyList<HopMove> Moves => _moves;

        public int MoveCount => _moves.Count;

        public int HintsUsed { get; private set; }

        public bool IsSolved => Current == Puzzle.Target;

        public bool CanUndo => _moves.Count > 0 && !IsSolved;

        public IEnumerable<NumberCard> Remaining => Puzzle.Hand.Where(c => _moves.All(m => m.Card.Id != c.Id));

        /// <summary>Jäljellä olevilla korteilla ei enää pääse tavoitteeseen.</summary>
        public bool IsDeadEnd => !IsSolved && Solve() == null;

        public int Stars => IsSolved ? CalculateStars(MoveCount, Puzzle.Par, HintsUsed) : 0;

        public CardError TryPlay(int cardId)
        {
            if (IsSolved)
                throw new InvalidOperationException("Pulma on jo ratkaistu.");

            NumberCard card = Remaining.FirstOrDefault(c => c.Id == cardId)
                ?? throw new ArgumentException("Korttia ei ole kädessä.", nameof(cardId));

            int before = Current;
            CardError error = card.TryApply(before, out int after);
            if (error == CardError.None)
                _moves.Add(new HopMove(card, before, after));
            return error;
        }

        public bool Undo()
        {
            if (!CanUndo)
                return false;
            _moves.RemoveAt(_moves.Count - 1);
            return true;
        }

        public void Restart() => _moves.Clear();

        /// <summary>Seuraava kortti lyhimmällä reitillä nykyisestä tilanteesta. Rajoittaa tähdet kahteen.</summary>
        public NumberCard? TakeHint()
        {
            IReadOnlyList<int>? path = Solve();
            if (path == null || path.Count == 0)
                return null;

            HintsUsed++;
            return Puzzle.Hand.First(c => c.Id == path[0]);
        }

        public static int CalculateStars(int moves, int par, int hintsUsed)
        {
            int stars = moves <= par ? 3 : moves <= par + 1 ? 2 : 1;
            return hintsUsed > 0 ? Math.Min(stars, 2) : stars;
        }

        public GameEvent ToEvent(GameOutcome outcome, Guid id, long durationMs, DateTimeOffset occurredAt)
        {
            if (outcome == GameOutcome.Solved && !IsSolved)
                throw new InvalidOperationException("Ratkaisematonta pulmaa ei voi tallentaa ratkaistuksi.");

            return new GameEvent
            {
                Id = id,
                GameId = NumberHopGame.GameId,
                SkillId = NumberHopGame.SkillId,
                Level = Puzzle.Level,
                Seed = Puzzle.Seed,
                Outcome = outcome,
                Moves = MoveCount,
                Par = Puzzle.Par,
                HintsUsed = HintsUsed,
                Stars = outcome == GameOutcome.Solved ? Stars : 0,
                DurationMs = durationMs,
                OccurredAt = occurredAt,
            };
        }

        private IReadOnlyList<int>? Solve()
        {
            int mask = 0;
            for (int i = 0; i < Puzzle.Hand.Count; i++)
            {
                if (_moves.Any(m => m.Card.Id == Puzzle.Hand[i].Id))
                    mask |= 1 << i;
            }

            return NumberHopSolver.Solve(Current, Puzzle.Target, Puzzle.Hand, mask);
        }
    }
}
