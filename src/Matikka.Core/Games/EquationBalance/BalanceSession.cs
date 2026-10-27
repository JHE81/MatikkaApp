using System;
using System.Collections.Generic;
using Matikka.Core.Progress;

namespace Matikka.Core.Games.EquationBalance
{
    /// <summary>
    /// Yhden tehtävän pelikerta: nykyinen tila, siirtohistoria, kumoaminen, vihjeet ja tähdet.
    /// Ei tiedä mitään käyttöliittymästä, joten sama luokka toimii MAUIssa, verkossa ja Unityssä.
    /// </summary>
    public sealed class BalanceSession
    {
        private readonly Stack<Equation> _history = new Stack<Equation>();
        private readonly List<BalanceMove> _moves = new List<BalanceMove>();

        public BalanceSession(BalancePuzzle puzzle)
        {
            Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
            Current = puzzle.Start;
        }

        public BalancePuzzle Puzzle { get; }

        public Equation Current { get; private set; }

        /// <summary>Tehdyt siirrot. Kumottu siirto poistuu listalta.</summary>
        public IReadOnlyList<BalanceMove> Moves => _moves;

        public int MoveCount => _moves.Count;

        public int HintsUsed { get; private set; }

        public bool IsSolved => Current.IsSolved;

        public bool CanUndo => _history.Count > 0 && !IsSolved;

        /// <summary>Tähdet 1–3. Nolla, jos tehtävä ei ole vielä ratkaistu.</summary>
        public int Stars => IsSolved ? CalculateStars(MoveCount, Puzzle.Par, HintsUsed) : 0;

        public MoveResult TryApply(BalanceMove move)
        {
            if (IsSolved)
                throw new InvalidOperationException("Tehtävä on jo ratkaistu.");

            MoveResult result = Current.Apply(move);
            if (result.Succeeded)
            {
                _history.Push(Current);
                _moves.Add(move);
                Current = result.Equation!;
            }

            return result;
        }

        public bool Undo()
        {
            if (!CanUndo)
                return false;

            Current = _history.Pop();
            _moves.RemoveAt(_moves.Count - 1);
            return true;
        }

        /// <summary>Seuraava järkevä siirto nykyisestä tilasta. Vihjeen käyttö rajoittaa tähdet kahteen.</summary>
        public BalanceMove? TakeHint()
        {
            BalanceMove? hint = BalanceSolver.NextMove(Current);
            if (hint != null)
                HintsUsed++;
            return hint;
        }

        /// <summary>
        /// Tallennettava tapahtuma tästä pelikerrasta. Id ja aika tulevat kutsujalta,
        /// jotta logiikka pysyy deterministisenä ja testattavana.
        /// </summary>
        public GameEvent ToEvent(GameOutcome outcome, Guid id, long durationMs, DateTimeOffset occurredAt)
        {
            if (outcome == GameOutcome.Solved && !IsSolved)
                throw new InvalidOperationException("Ratkaisematonta tehtävää ei voi tallentaa ratkaistuksi.");

            return new GameEvent
            {
                Id = id,
                GameId = EquationBalanceGame.GameId,
                SkillId = EquationBalanceGame.SkillId,
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

        /// <summary>
        /// Golf-tyylinen pisteytys: par-siirroilla 3 tähteä, enintään kaksi ylimääräistä 2 tähteä,
        /// muuten 1. Tehtävästä ei voi epäonnistua, se vain ratkeaa pidemmän kautta.
        /// </summary>
        public static int CalculateStars(int moves, int par, int hintsUsed)
        {
            int stars = moves <= par ? 3 : moves <= par + 2 ? 2 : 1;
            return hintsUsed > 0 ? Math.Min(stars, 2) : stars;
        }
    }
}
