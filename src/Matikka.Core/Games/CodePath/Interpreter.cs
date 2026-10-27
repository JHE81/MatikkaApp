using System;
using System.Collections.Generic;
using System.Linq;

namespace Matikka.Core.Games.CodePath
{
    public enum RunOutcome
    {
        /// <summary>Hahmo pääsi pesään kaikki mustikat kerättyinä.</summary>
        Success,

        /// <summary>Hahmo yritti kävellä puuhun tai kartan reunan yli.</summary>
        Bump,

        /// <summary>Ohjelma loppui ennen pesää.</summary>
        EndedBeforeGoal,

        /// <summary>Ohjelma loppui pesään, mutta mustikoita jäi keräämättä.</summary>
        MissingBerries,

        /// <summary>Askelia tuli liikaa: luultavasti aliohjelma kutsuu itseään loputtomasti.</summary>
        TooManySteps,

        /// <summary>Ohjelmassa ei ole yhtään liikettä tuottavaa lohkoa.</summary>
        Empty,
    }

    public enum StepAction
    {
        Forward,
        TurnLeft,
        TurnRight,
        Bump,
    }

    /// <param name="BlockId">Lohko, joka tuotti askeleen (korostusta varten).</param>
    /// <param name="Action">Mitä tapahtui.</param>
    /// <param name="Position">Sijainti askeleen jälkeen.</param>
    /// <param name="Heading">Suunta askeleen jälkeen.</param>
    /// <param name="CollectedBerry">Kerätyn mustikan indeksi, jos askeleella kerättiin.</param>
    public sealed record RunStep(int BlockId, StepAction Action, Cell Position, Heading Heading, int? CollectedBerry);

    public sealed class RunResult
    {
        public RunResult(RunOutcome outcome, IReadOnlyList<RunStep> steps, int? failedBlockId, int berriesCollected)
        {
            Outcome = outcome;
            Steps = steps;
            FailedBlockId = failedBlockId;
            BerriesCollected = berriesCollected;
        }

        public RunOutcome Outcome { get; }

        public IReadOnlyList<RunStep> Steps { get; }

        /// <summary>Lohko, jossa ajo päättyi virheeseen (törmäys).</summary>
        public int? FailedBlockId { get; }

        public int BerriesCollected { get; }
    }

    /// <summary>
    /// Ajaa ohjelman kartalla ja tallentaa jokaisen askeleen. Käyttöliittymä animoi askeleet
    /// jälkikäteen, joten logiikka on testattavissa ilman ajastimia.
    /// </summary>
    public static class Interpreter
    {
        public const int MaxSteps = 300;

        /// <summary>Raja myös liikkumattomille lohkoille, esim. aliohjelma, joka vain kutsuu itseään toistossa.</summary>
        public const int MaxOperations = 5000;
        private const int MaxCallDepth = 30;

        public static RunResult Run(RouteMap map, RobotProgram program) =>
            Run(map, program.Main, program.Procedure);

        public static RunResult Run(RouteMap map, IReadOnlyList<Block> main, IReadOnlyList<Block> procedure)
        {
            var run = new Execution(map, procedure);
            run.Execute(main, 0);

            RunOutcome outcome;
            if (run.Outcome != null)
                outcome = run.Outcome.Value;
            else if (run.Steps.Count == 0)
                outcome = RunOutcome.Empty;
            else if (run.Position == map.Goal)
                outcome = RunOutcome.MissingBerries;
            else
                outcome = RunOutcome.EndedBeforeGoal;

            return new RunResult(outcome, run.Steps, run.FailedBlockId, run.Collected.Count);
        }

        private sealed class Execution
        {
            private readonly RouteMap _map;
            private readonly IReadOnlyList<Block> _procedure;

            public Execution(RouteMap map, IReadOnlyList<Block> procedure)
            {
                _map = map;
                _procedure = procedure;
                Position = map.Start;
                Heading = map.StartHeading;
            }

            public List<RunStep> Steps { get; } = new List<RunStep>();

            public HashSet<int> Collected { get; } = new HashSet<int>();

            public Cell Position { get; private set; }

            public Heading Heading { get; private set; }

            public RunOutcome? Outcome { get; private set; }

            public int? FailedBlockId { get; private set; }

            /// <summary>Palauttaa false, kun ajo päättyy (onnistui tai epäonnistui).</summary>
            public bool Execute(IReadOnlyList<Block> blocks, int depth)
            {
                foreach (Block block in blocks)
                {
                    if (!ExecuteBlock(block, depth))
                        return false;
                }

                return true;
            }

            private int _operations;

            private bool ExecuteBlock(Block block, int depth)
            {
                if (++_operations > MaxOperations)
                    return Stop(RunOutcome.TooManySteps, null);

                switch (block.Kind)
                {
                    case CommandKind.Repeat:
                        for (int i = 0; i < block.Count; i++)
                        {
                            if (!Execute(block.Body, depth))
                                return false;
                        }

                        return true;

                    case CommandKind.Call:
                        if (depth >= MaxCallDepth)
                            return Stop(RunOutcome.TooManySteps, null);
                        return Execute(_procedure, depth + 1);

                    case CommandKind.TurnLeft:
                        Heading = Heading.Left();
                        return Record(block.Id, StepAction.TurnLeft, null);

                    case CommandKind.TurnRight:
                        Heading = Heading.Right();
                        return Record(block.Id, StepAction.TurnRight, null);

                    default:
                    {
                        Cell next = Position.Step(Heading);
                        if (!_map.IsFree(next))
                        {
                            Steps.Add(new RunStep(block.Id, StepAction.Bump, Position, Heading, null));
                            return Stop(RunOutcome.Bump, block.Id);
                        }

                        Position = next;
                        int? berry = null;
                        for (int i = 0; i < _map.Berries.Count; i++)
                        {
                            if (_map.Berries[i] == next && Collected.Add(i))
                                berry = i;
                        }

                        if (!Record(block.Id, StepAction.Forward, berry))
                            return false;

                        if (Position == _map.Goal && Collected.Count == _map.Berries.Count)
                            return Stop(RunOutcome.Success, null);

                        return true;
                    }
                }
            }

            private bool Record(int blockId, StepAction action, int? berry)
            {
                Steps.Add(new RunStep(blockId, action, Position, Heading, berry));
                return Steps.Count < MaxSteps || Stop(RunOutcome.TooManySteps, null);
            }

            private bool Stop(RunOutcome outcome, int? failedBlockId)
            {
                Outcome = outcome;
                FailedBlockId = failedBlockId;
                return false;
            }
        }
    }
}
