using System;
using Matikka.Core.Progress;

namespace Matikka.Core.Games.CodePath
{
    /// <summary>
    /// Yksi kenttä: pelaajan ohjelma, ajot ja tulos. Epäonnistuneet ajot eivät vähennä tähtiä:
    /// virheiden etsiminen ja korjaaminen on osa algoritmista ajattelua.
    /// </summary>
    public sealed class CodePathSession
    {
        public CodePathSession(CodePathPuzzle puzzle)
        {
            Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
        }

        public CodePathPuzzle Puzzle { get; }

        public RobotProgram Program { get; } = new RobotProgram();

        public RunResult? LastRun { get; private set; }

        public int Runs { get; private set; }

        public int HintsUsed { get; private set; }

        public bool IsSolved { get; private set; }

        /// <summary>Ratkaisun lohkomäärä (tähdet lasketaan tästä).</summary>
        public int SolvedSize { get; private set; }

        public int Stars => IsSolved ? CalculateStars(SolvedSize, Puzzle.Par, HintsUsed) : 0;

        public bool IsOverLimit => Puzzle.BlockLimit is int limit && Program.Size > limit;

        public bool CanAdd => !IsSolved && (Puzzle.BlockLimit is not int limit || Program.Size < limit);

        public RunResult Run()
        {
            if (IsSolved)
                throw new InvalidOperationException("Kenttä on jo ratkaistu.");

            Runs++;
            LastRun = Interpreter.Run(Puzzle.Map, Program);
            if (LastRun.Outcome == RunOutcome.Success && !IsOverLimit)
            {
                IsSolved = true;
                SolvedSize = Program.Size;
            }

            return LastRun;
        }

        /// <summary>Näyttää malliratkaisun reitin kartalla. Vihje rajoittaa tähdet kahteen.</summary>
        public void TakeHint() => HintsUsed++;

        /// <summary>Malliratkaisun kokoinen tai pienempi 3 tähteä, enintään kaksi lohkoa yli 2, muuten 1.</summary>
        public static int CalculateStars(int size, int par, int hintsUsed)
        {
            int stars = size <= par ? 3 : size <= par + 2 ? 2 : 1;
            return hintsUsed > 0 ? Math.Min(stars, 2) : stars;
        }

        public GameEvent ToEvent(GameOutcome outcome, Guid id, long durationMs, DateTimeOffset occurredAt)
        {
            if (outcome == GameOutcome.Solved && !IsSolved)
                throw new InvalidOperationException("Ratkaisematonta kenttää ei voi tallentaa ratkaistuksi.");

            return new GameEvent
            {
                Id = id,
                GameId = CodePathGame.GameId,
                SkillId = CodePathGame.SkillId,
                Level = Puzzle.Level,
                Seed = Puzzle.Seed,
                Outcome = outcome,
                Moves = outcome == GameOutcome.Solved ? SolvedSize : Program.Size,
                Par = Puzzle.Par,
                HintsUsed = HintsUsed,
                Stars = outcome == GameOutcome.Solved ? Stars : 0,
                DurationMs = durationMs,
                OccurredAt = occurredAt,
            };
        }
    }
}
