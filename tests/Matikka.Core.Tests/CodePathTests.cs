using Matikka.Core.Adaptivity;
using Matikka.Core.Games.CodePath;
using Matikka.Core.Progress;

namespace Matikka.Core.Tests;

public class CodePathTests
{
    public static TheoryData<int> AllLevels()
    {
        var data = new TheoryData<int>();
        for (int level = 1; level <= CodePathGenerator.MaxLevel; level++)
            data.Add(level);
        return data;
    }

    /// <summary>Käytävä: lähtö (0,0) itään, pesä (3,0). Puut rivillä 1.</summary>
    private static RouteMap Corridor(params Cell[] berries) =>
        new RouteMap(4, 2, new[] { new Cell(0, 1), new Cell(1, 1), new Cell(2, 1), new Cell(3, 1) },
            new Cell(0, 0), Heading.East, new Cell(3, 0), berries);

    private static RobotProgram Program(params CommandKind[] main)
    {
        var program = new RobotProgram();
        foreach (CommandKind kind in main)
            program.Main.Add(program.NewBlock(kind));
        return program;
    }

    /// <summary>Kopioi malliratkaisun pelaajan ohjelmaan (kuten pelaaja sen kokoaisi).</summary>
    private static void CopyModel(CodePathPuzzle puzzle, RobotProgram target)
    {
        void Copy(IEnumerable<Block> from, List<Block> to)
        {
            foreach (Block b in from)
            {
                Block copy = target.NewBlock(b.Kind, b.Count);
                if (b.Kind == CommandKind.Repeat)
                    Copy(b.Body, copy.Body);
                to.Add(copy);
            }
        }

        Copy(puzzle.ModelMain, target.Main);
        Copy(puzzle.ModelProcedure, target.Procedure);
    }

    [Fact]
    public void Straight_program_reaches_goal()
    {
        var result = Interpreter.Run(Corridor(), Program(CommandKind.Forward, CommandKind.Forward, CommandKind.Forward));

        Assert.Equal(RunOutcome.Success, result.Outcome);
        Assert.Equal(3, result.Steps.Count);
        Assert.Equal(new Cell(3, 0), result.Steps[^1].Position);
    }

    [Fact]
    public void Walking_into_tree_stops_at_failing_block()
    {
        var program = Program(CommandKind.TurnRight, CommandKind.Forward, CommandKind.Forward);

        var result = Interpreter.Run(Corridor(), program);

        Assert.Equal(RunOutcome.Bump, result.Outcome);
        Assert.Equal(program.Main[1].Id, result.FailedBlockId);
        Assert.Equal(StepAction.Bump, result.Steps[^1].Action);
    }

    [Fact]
    public void Stopping_short_and_missing_berries_are_reported()
    {
        Assert.Equal(RunOutcome.EndedBeforeGoal, Interpreter.Run(Corridor(), Program(CommandKind.Forward)).Outcome);
        Assert.Equal(RunOutcome.Empty, Interpreter.Run(Corridor(), new RobotProgram()).Outcome);

        // Mustikka on reitin sivussa: suora ohjelma vie pesään ilman sitä.
        var map = new RouteMap(4, 2, Array.Empty<Cell>(), new Cell(0, 0), Heading.East, new Cell(3, 0), new[] { new Cell(1, 1) });
        var result = Interpreter.Run(map, Program(CommandKind.Forward, CommandKind.Forward, CommandKind.Forward));
        Assert.Equal(RunOutcome.MissingBerries, result.Outcome);
    }

    [Fact]
    public void Repeat_and_procedure_expand_correctly()
    {
        var program = new RobotProgram();
        Block repeat = program.NewBlock(CommandKind.Repeat, 3);
        repeat.Body.Add(program.NewBlock(CommandKind.Forward));
        program.Procedure.Add(repeat);
        program.Main.Add(program.NewBlock(CommandKind.Call));

        var result = Interpreter.Run(Corridor(), program);

        Assert.Equal(RunOutcome.Success, result.Outcome);
        Assert.Equal(3, program.Size);
    }

    [Fact]
    public void Endless_recursion_is_stopped()
    {
        var program = new RobotProgram();
        program.Procedure.Add(program.NewBlock(CommandKind.TurnLeft));
        program.Procedure.Add(program.NewBlock(CommandKind.Call));
        program.Main.Add(program.NewBlock(CommandKind.Call));

        Assert.Equal(RunOutcome.TooManySteps, Interpreter.Run(Corridor(), program).Outcome);

        // Kutsu toiston sisällä ilman liikettä: räjähtäisi ilman operaatiorajaa.
        var bomb = new RobotProgram();
        Block loop = bomb.NewBlock(CommandKind.Repeat, 9);
        loop.Body.Add(bomb.NewBlock(CommandKind.Call));
        bomb.Procedure.Add(loop);
        bomb.Main.Add(bomb.NewBlock(CommandKind.Call));
        Assert.Equal(RunOutcome.TooManySteps, Interpreter.Run(Corridor(), bomb).Outcome);
    }

    [Fact]
    public void Repeat_count_is_clamped()
    {
        var program = new RobotProgram();
        Block b = program.NewBlock(CommandKind.Repeat, 50);
        Assert.Equal(Block.MaxRepeat, b.Count);
        b.Count = 0;
        Assert.Equal(Block.MinRepeat, b.Count);
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Every_map_is_solved_by_its_model_within_limit(int level)
    {
        for (long seed = 0; seed < 300; seed++)
        {
            CodePathPuzzle puzzle = CodePathGenerator.Generate(level, seed);
            var session = new CodePathSession(puzzle);
            CopyModel(puzzle, session.Program);

            Assert.Equal(puzzle.Par, session.Program.Size);
            Assert.Equal(RunOutcome.Success, session.Run().Outcome);
            Assert.True(session.IsSolved);
            Assert.Equal(3, session.Stars);

            List<CommandKind> shortest = CodePathGenerator.ShortestSequence(puzzle.Map)!;
            if (level <= 2)
            {
                Assert.Null(puzzle.BlockLimit);
                Assert.Equal(shortest.Count, puzzle.Par); // par on tarkka
            }
            else
            {
                // Pelkät peruslohkot eivät mahdu rajaan: toisto/aliohjelma on pakko keksiä.
                Assert.True(shortest.Count > puzzle.BlockLimit!.Value);
            }
        }
    }

    [Fact]
    public void Levels_introduce_blocks_in_order()
    {
        for (long seed = 0; seed < 100; seed++)
        {
            Assert.DoesNotContain(CommandKind.Repeat, CodePathGenerator.Generate(1, seed).Palette);
            Assert.Equal(2, CodePathGenerator.Generate(2, seed).Map.Berries.Count);
            Assert.Contains(CommandKind.Repeat, CodePathGenerator.Generate(3, seed).Palette);
            Assert.DoesNotContain(CommandKind.Call, CodePathGenerator.Generate(4, seed).Palette);
            Assert.True(CodePathGenerator.Generate(5, seed).ModelProcedure.Count > 0);
            Assert.Contains(CodePathGenerator.Generate(6, seed).ModelMain, b => b.Kind == CommandKind.Repeat);
        }
    }

    [Fact]
    public void Same_level_and_seed_give_the_same_map()
    {
        var a = CodePathGenerator.Generate(4, 31);
        var b = CodePathGenerator.Generate(4, 31);

        Assert.Equal(a.Map.Start, b.Map.Start);
        Assert.Equal(a.Map.Goal, b.Map.Goal);
        Assert.True(a.Map.Trees.SequenceEqual(b.Map.Trees));
        Assert.Equal(a.Par, b.Par);
    }

    [Fact]
    public void Over_limit_program_does_not_count_as_solved()
    {
        var puzzle = CodePathGenerator.Generate(3, 5);
        var session = new CodePathSession(puzzle);
        foreach (CommandKind command in CodePathGenerator.ShortestSequence(puzzle.Map)!)
            session.Program.Main.Add(session.Program.NewBlock(command));

        RunResult run = session.Run();

        Assert.Equal(RunOutcome.Success, run.Outcome); // reitti on oikea...
        Assert.True(session.IsOverLimit);
        Assert.False(session.IsSolved);                 // ...mutta lohkoja on liikaa
        Assert.False(session.CanAdd);
    }

    [Fact]
    public void Hint_and_extra_blocks_reduce_stars()
    {
        Assert.Equal(3, CodePathSession.CalculateStars(5, 5, 0));
        Assert.Equal(2, CodePathSession.CalculateStars(7, 5, 0));
        Assert.Equal(1, CodePathSession.CalculateStars(8, 5, 0));
        Assert.Equal(2, CodePathSession.CalculateStars(4, 5, 1));
    }

    [Fact]
    public void Solved_event_grows_algorithm_skill()
    {
        var puzzle = CodePathGenerator.Generate(1, 9);
        var session = new CodePathSession(puzzle);
        CopyModel(puzzle, session.Program);
        session.Run();

        GameEvent e = session.ToEvent(GameOutcome.Solved, Guid.NewGuid(), 40_000, DateTimeOffset.Now);
        var model = ProgressModel.Replay(new[] { e });

        Assert.Equal(CodePathGame.SkillId, e.SkillId);
        Assert.Equal(puzzle.Par, e.Moves);
        Assert.Equal(2, LevelSelector.HighestUnlocked(CodePathGame.Info, model));
    }
}
