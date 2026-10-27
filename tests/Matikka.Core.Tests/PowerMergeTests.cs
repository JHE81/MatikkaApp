using Matikka.Core.Adaptivity;
using Matikka.Core.Games.PowerMerge;
using Matikka.Core.Progress;

namespace Matikka.Core.Tests;

public class PowerMergeTests
{
    private static PowerTile P(int id, char b, int e) => PowerTile.Power(id, b, e);

    /// <summary>4×4-lauta, jonka ylärivillä annetut laatat.</summary>
    private static PowerBoard TopRow(params PowerTile?[] row)
    {
        var cells = new PowerTile?[16];
        for (int i = 0; i < row.Length; i++)
            cells[i] = row[i];
        return new PowerBoard(4, cells);
    }

    private static PowerTile Combine(PowerTile a, PowerTile b, out MergeRule rule)
    {
        Assert.True(PowerRules.TryCombine(a, b, out PowerTile? result, out rule));
        return result!;
    }

    public static TheoryData<int> AllLevels()
    {
        var data = new TheoryData<int>();
        for (int level = 1; level <= PowerPuzzleGenerator.MaxLevel; level++)
            data.Add(level);
        return data;
    }

    [Fact]
    public void Product_adds_exponents_and_keeps_moving_tiles_id()
    {
        PowerTile result = Combine(P(1, 'a', 2), P(2, 'a', 3), out MergeRule rule);

        Assert.Equal(PowerTile.Power(2, 'a', 5), result);
        Assert.Equal(MergeRule.Product, rule);
    }

    [Fact]
    public void Division_subtracts_and_equal_powers_give_one()
    {
        Assert.Equal(PowerTile.Power(2, 'a', 3), Combine(P(1, 'a', 5), P(2, 'a', -2), out _));
        Assert.Equal(PowerTile.Power(2, 'a', -2), Combine(P(1, 'a', 1), P(2, 'a', -3), out _));
        Assert.Equal(TileKind.One, Combine(P(1, 'a', 3), P(2, 'a', -3), out _).Kind);
    }

    [Fact]
    public void Raise_multiplies_exponent()
    {
        Assert.Equal(PowerTile.Power(2, 'a', 6), Combine(PowerTile.Raise(1, 3), P(2, 'a', 2), out MergeRule rule));
        Assert.Equal(MergeRule.PowerOfPower, rule);
        Assert.Equal(PowerTile.Power(2, 'b', -4), Combine(P(1, 'b', -2), PowerTile.Raise(2, 2), out _));
        Assert.Equal(PowerTile.Raise(2, 6), Combine(PowerTile.Raise(1, 2), PowerTile.Raise(2, 3), out MergeRule compose));
        Assert.Equal(MergeRule.ComposeRaises, compose);
    }

    [Fact]
    public void One_is_neutral()
    {
        Assert.Equal(PowerTile.Power(2, 'a', 4), Combine(PowerTile.One(1), P(2, 'a', 4), out MergeRule rule));
        Assert.Equal(MergeRule.Identity, rule);
        Assert.Equal(TileKind.One, Combine(PowerTile.Raise(1, 3), PowerTile.One(2), out _).Kind);
    }

    [Fact]
    public void Different_bases_do_not_combine()
    {
        Assert.False(PowerRules.TryCombine(P(1, 'a', 2), P(2, 'b', 2), out _, out _));
    }

    [Fact]
    public void Each_tile_merges_once_per_move()
    {
        // a · a² = a³, ja vanha a³ jää viereen: kaksi a³:ta, ei a⁶:ta.
        var board = TopRow(P(1, 'a', 1), P(2, 'a', 2), null, P(3, 'a', 3));

        SlideResult slide = board.Slide(Direction.Left);

        Assert.True(slide.Changed);
        Assert.Single(slide.Merges);
        Assert.Equal(PowerTile.Power(2, 'a', 3), slide.Board[0, 0]);
        Assert.Equal(PowerTile.Power(3, 'a', 3), slide.Board[0, 1]);
        Assert.Null(slide.Board[0, 2]);
    }

    [Fact]
    public void Sliding_right_merges_from_the_right_wall()
    {
        var board = TopRow(P(1, 'a', 1), P(2, 'a', 2), P(3, 'a', 3));

        SlideResult slide = board.Slide(Direction.Right);

        Assert.Equal(PowerTile.Power(2, 'a', 5), slide.Board[0, 3]);
        Assert.Equal(PowerTile.Power(1, 'a', 1), slide.Board[0, 2]);
    }

    [Fact]
    public void Columns_slide_up_and_down()
    {
        var cells = new PowerTile?[16];
        cells[1] = P(1, 'b', 2);  // rivi 0, sarake 1
        cells[13] = P(2, 'b', 4); // rivi 3, sarake 1
        var board = new PowerBoard(4, cells);

        Assert.Equal(PowerTile.Power(1, 'b', 6), board.Slide(Direction.Down).Board[3, 1]);
        Assert.Equal(PowerTile.Power(2, 'b', 6), board.Slide(Direction.Up).Board[0, 1]);
    }

    [Fact]
    public void Blocked_move_changes_nothing()
    {
        var board = TopRow(P(1, 'a', 2), P(2, 'b', 2));

        SlideResult slide = board.Slide(Direction.Left);

        Assert.False(slide.Changed);
        Assert.Empty(slide.Merges);
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Every_puzzle_is_solvable_in_exactly_par_moves(int level)
    {
        var (parMin, parMax) = PowerPuzzleGenerator.ParRange(level);

        for (long seed = 0; seed < 300; seed++)
        {
            PowerPuzzle puzzle = PowerPuzzleGenerator.Generate(level, seed);

            Assert.InRange(puzzle.Par, parMin, parMax);
            Assert.True(puzzle.Goals.All(g => !puzzle.Start.Contains(g)));

            SolveResult solve = PowerSolver.Solve(puzzle.Start, puzzle.Goals);
            Assert.Equal(Reachability.Reachable, solve.Status);
            Assert.Equal(puzzle.Par, solve.Path.Count);

            PowerBoard board = puzzle.Start;
            foreach (Direction move in solve.Path)
                board = board.Slide(move).Board;
            Assert.True(board.ContainsAll(puzzle.Goals));
        }
    }

    [Fact]
    public void Levels_introduce_rules_in_order()
    {
        for (long seed = 0; seed < 200; seed++)
        {
            var l1 = PowerPuzzleGenerator.Generate(1, seed);
            Assert.True(l1.Start.Tiles.All(t => t.Tile.Kind == TileKind.Power && t.Tile.Base == 'a' && t.Tile.Exponent > 0));

            var l2 = PowerPuzzleGenerator.Generate(2, seed);
            Assert.True(l2.Start.Tiles.All(t => t.Tile.Kind == TileKind.Power && t.Tile.Exponent > 0));

            Assert.Equal(TileKind.One, PowerPuzzleGenerator.Generate(4, seed).Goals[0].Kind);
            Assert.Contains(PowerPuzzleGenerator.Generate(5, seed).Start.Tiles, t => t.Tile.Kind == TileKind.Raise);

            var l6 = PowerPuzzleGenerator.Generate(6, seed);
            Assert.True(l6.NegativeNotation);
            Assert.True(l6.Goals[0].Exponent < 0);

            var l7 = PowerPuzzleGenerator.Generate(7, seed);
            Assert.Equal(2, l7.Goals.Count);
            Assert.NotEqual(l7.Goals[0].Base, l7.Goals[1].Base);
        }
    }

    [Fact]
    public void Same_level_and_seed_give_the_same_puzzle()
    {
        var a = PowerPuzzleGenerator.Generate(5, 2024);
        var b = PowerPuzzleGenerator.Generate(5, 2024);

        Assert.Equal(a.Start.Key, b.Start.Key);
        Assert.True(a.Goals.SequenceEqual(b.Goals));
        Assert.Equal(a.Par, b.Par);
    }

    [Fact]
    public void Following_hints_solves_in_par_with_two_stars()
    {
        for (long seed = 0; seed < 50; seed++)
        {
            var session = new PowerSession(PowerPuzzleGenerator.Generate(7, seed));
            while (!session.IsSolved)
            {
                Direction? hint = session.TakeHint();
                Assert.NotNull(hint);
                Assert.True(session.TryMove(hint!.Value));
            }

            Assert.Equal(session.Puzzle.Par, session.MoveCount);
            Assert.Equal(2, session.Stars);
        }
    }

    [Fact]
    public void Wrong_move_can_lead_to_dead_end_and_undo_recovers()
    {
        // a² a³ a: oikealle a · a³ = a⁴ (tavoite). Vasemmalle a² · a³ = a⁵, eikä a⁴:ää saa enää.
        var start = TopRow(P(1, 'a', 2), P(2, 'a', 3), P(3, 'a', 1));
        var session = new PowerSession(new PowerPuzzle(1, 0, start, new[] { PowerGoal.Of('a', 4) }, 1, false));

        Assert.False(session.IsDeadEnd);
        Assert.True(session.TryMove(Direction.Left));
        Assert.True(session.IsDeadEnd);
        Assert.Null(session.TakeHint());

        Assert.True(session.Undo());
        Assert.False(session.IsDeadEnd);
        Assert.Equal(Direction.Right, session.TakeHint());
    }

    [Fact]
    public void Restart_returns_to_start_and_resets_moves()
    {
        var session = new PowerSession(PowerPuzzleGenerator.Generate(3, 11));
        Direction first = PowerSolver.Directions.First(d => session.Current.Slide(d).Changed);
        session.TryMove(first);

        session.Restart();

        Assert.Equal(session.Puzzle.Start.Key, session.Current.Key);
        Assert.Equal(0, session.MoveCount);
        Assert.Empty(session.LastMerges);
    }

    [Fact]
    public void Solved_event_grows_power_skill()
    {
        var session = new PowerSession(PowerPuzzleGenerator.Generate(1, 3));
        foreach (Direction move in PowerSolver.Solve(session.Current, session.Puzzle.Goals).Path)
            session.TryMove(move);

        GameEvent e = session.ToEvent(GameOutcome.Solved, Guid.NewGuid(), 20_000, DateTimeOffset.Now);
        var model = ProgressModel.Replay(new[] { e });

        Assert.Equal(PowerMergeGame.SkillId, e.SkillId);
        Assert.Equal(3, e.Stars);
        Assert.True(model.GetSkill(PowerMergeGame.Info).Rating > PowerMergeGame.Info.InitialRating);
        Assert.Equal(2, LevelSelector.HighestUnlocked(PowerMergeGame.Info, model));
    }
}
