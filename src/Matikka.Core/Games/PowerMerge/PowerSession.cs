using System;
using System.Collections.Generic;
using System.Linq;
using Matikka.Core.Progress;

namespace Matikka.Core.Games.PowerMerge
{
    /// <summary>Yksi pulma: lauta, siirtohistoria, kumoaminen, alusta aloitus, vihjeet ja umpikujan tunnistus.</summary>
    public sealed class PowerSession
    {
        private readonly Stack<PowerBoard> _history = new Stack<PowerBoard>();
        private SolveResult? _solve;

        public PowerSession(PowerPuzzle puzzle)
        {
            Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
            Current = puzzle.Start;
        }

        public PowerPuzzle Puzzle { get; }

        public PowerBoard Current { get; private set; }

        /// <summary>Viimeisimmän siirron yhdistymiset (laskusääntöjen näyttämistä varten).</summary>
        public IReadOnlyList<MergeInfo> LastMerges { get; private set; } = Array.Empty<MergeInfo>();

        public int MoveCount => _history.Count;

        public int HintsUsed { get; private set; }

        public bool IsSolved => Current.ContainsAll(Puzzle.Goals);

        public bool CanUndo => _history.Count > 0 && !IsSolved;

        /// <summary>Tavoitetta ei saa enää koottua: pelaajan pitää kumota tai aloittaa alusta.</summary>
        public bool IsDeadEnd => !IsSolved && Solve().Status == Reachability.Unreachable;

        public int Stars => IsSolved ? CalculateStars(MoveCount, Puzzle.Par, HintsUsed) : 0;

        /// <summary>Tunnisteet laatoille, jotka täyttävät tavoitteen (korostusta varten).</summary>
        public IReadOnlyCollection<int> GoalTileIds =>
            Current.Tiles.Where(t => Puzzle.Goals.Any(g => g.Matches(t.Tile))).Select(t => t.Tile.Id).ToList();

        /// <summary>Palauttaa false, jos siirto ei muuta lautaa (ei lasketa siirroksi).</summary>
        public bool TryMove(Direction direction)
        {
            if (IsSolved)
                throw new InvalidOperationException("Pulma on jo ratkaistu.");

            SlideResult slide = Current.Slide(direction);
            if (!slide.Changed)
                return false;

            _history.Push(Current);
            Current = slide.Board;
            LastMerges = slide.Merges;
            _solve = null;
            return true;
        }

        public bool Undo()
        {
            if (!CanUndo)
                return false;

            Current = _history.Pop();
            LastMerges = Array.Empty<MergeInfo>();
            _solve = null;
            return true;
        }

        public void Restart()
        {
            _history.Clear();
            Current = Puzzle.Start;
            LastMerges = Array.Empty<MergeInfo>();
            _solve = null;
        }

        /// <summary>Lyhimmän ratkaisun seuraava siirto. Vihje rajoittaa tähdet kahteen.</summary>
        public Direction? TakeHint()
        {
            if (IsSolved)
                return null;

            SolveResult solve = Solve();
            if (solve.Status != Reachability.Reachable || solve.Path.Count == 0)
                return null;

            HintsUsed++;
            return solve.Path[0];
        }

        /// <summary>Sama golf-pisteytys kuin Yhtälövaa'assa.</summary>
        public static int CalculateStars(int moves, int par, int hintsUsed)
        {
            int stars = moves <= par ? 3 : moves <= par + 2 ? 2 : 1;
            return hintsUsed > 0 ? Math.Min(stars, 2) : stars;
        }

        public GameEvent ToEvent(GameOutcome outcome, Guid id, long durationMs, DateTimeOffset occurredAt)
        {
            if (outcome == GameOutcome.Solved && !IsSolved)
                throw new InvalidOperationException("Ratkaisematonta pulmaa ei voi tallentaa ratkaistuksi.");

            return new GameEvent
            {
                Id = id,
                GameId = PowerMergeGame.GameId,
                SkillId = PowerMergeGame.SkillId,
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

        private SolveResult Solve() => _solve ??= PowerSolver.Solve(Current, Puzzle.Goals);
    }
}
