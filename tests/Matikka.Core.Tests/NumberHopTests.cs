using Matikka.Core.Adaptivity;
using Matikka.Core.Games.NumberHop;
using Matikka.Core.Progress;

namespace Matikka.Core.Tests;

public class NumberHopTests
{
    public static TheoryData<int> AllLevels()
    {
        var data = new TheoryData<int>();
        for (int level = 1; level <= NumberHopGenerator.MaxLevel; level++)
            data.Add(level);
        return data;
    }

    private static int Apply(NumberCard card, int value)
    {
        Assert.Equal(CardError.None, card.TryApply(value, out int result));
        return result;
    }

    [Theory]
    [InlineData(CardOp.Add, -4, 3, -1)]
    [InlineData(CardOp.Subtract, -3, 2, 5)]
    [InlineData(CardOp.Multiply, -1, 7, -7)]
    [InlineData(CardOp.Multiply, -2, -3, 6)]
    [InlineData(CardOp.Divide, -2, 8, -4)]
    [InlineData(CardOp.Square, 0, -3, 9)]
    [InlineData(CardOp.Cube, 0, -3, -27)]
    public void Cards_follow_sign_rules(CardOp op, int operand, int value, int expected)
    {
        Assert.Equal(expected, Apply(new NumberCard(1, op, operand), value));
    }

    [Fact]
    public void Inexact_division_and_out_of_range_are_rejected()
    {
        Assert.Equal(CardError.NotDivisible, new NumberCard(1, CardOp.Divide, 2).TryApply(7, out _));
        Assert.Equal(CardError.OutOfRange, new NumberCard(1, CardOp.Cube).TryApply(-5, out _));
    }

    [Fact]
    public void Labels_and_descriptions_use_brackets_for_negatives()
    {
        var card = new NumberCard(1, CardOp.Subtract, -3);
        Assert.Equal("− (−3)", card.Label);
        Assert.Equal("−2 − (−3) = 1", card.Describe(-2, 1));
        Assert.Equal("(−4)² = 16", new NumberCard(2, CardOp.Square).Describe(-4, 16));
    }

    [Fact]
    public void Solver_uses_each_card_once_and_order_matters()
    {
        // 2 → −10: ensin +3 sitten ·(−2). Toisin päin −4 + 3 = −1.
        var hand = new[] { new NumberCard(1, CardOp.Multiply, -2), new NumberCard(2, CardOp.Add, 3) };

        Assert.True(NumberHopSolver.Solve(2, -10, hand)!.SequenceEqual(new[] { 2, 1 }));
        Assert.Null(NumberHopSolver.Solve(2, 100, hand));
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Every_puzzle_is_solvable_in_exactly_par_cards(int level)
    {
        var (parMin, parMax) = NumberHopGenerator.ParRange(level);
        for (long seed = 0; seed < 400; seed++)
        {
            NumberHopPuzzle puzzle = NumberHopGenerator.Generate(level, seed);
            Assert.NotEqual(puzzle.Start, puzzle.Target);
            Assert.InRange(puzzle.Par, parMin, parMax);
            Assert.Equal(puzzle.Hand.Count, puzzle.Hand.Select(c => c.Id).Distinct().Count());

            var session = new NumberHopSession(puzzle);
            foreach (int id in NumberHopSolver.Solve(puzzle.Start, puzzle.Target, puzzle.Hand)!)
                Assert.Equal(CardError.None, session.TryPlay(id));

            Assert.True(session.IsSolved);
            Assert.Equal(puzzle.Par, session.MoveCount);
            Assert.Equal(3, session.Stars);
        }
    }

    [Fact]
    public void Level_content_appears_in_the_solution()
    {
        for (long seed = 0; seed < 200; seed++)
        {
            var l3 = NumberHopGenerator.Generate(3, seed);
            var path3 = NumberHopSolver.Solve(l3.Start, l3.Target, l3.Hand)!.Select(id => l3.Hand.First(c => c.Id == id));
            Assert.Contains(path3, c => c.Op == CardOp.Multiply && c.Operand < 0);

            var l5 = NumberHopGenerator.Generate(5, seed);
            Assert.Contains(l5.Hand, c => c.Op is CardOp.Square or CardOp.Cube);
        }
    }

    [Fact]
    public void Hint_undo_and_dead_end()
    {
        // −2 → 4: neliö riittää. Jos ensin pelaa +5 (= 3), tavoitteeseen ei enää pääse.
        var hand = new[] { new NumberCard(1, CardOp.Square), new NumberCard(2, CardOp.Add, 5) };
        var session = new NumberHopSession(new NumberHopPuzzle(5, 0, -2, 4, hand, 1));

        Assert.Equal(CardError.None, session.TryPlay(2));
        Assert.True(session.IsDeadEnd);
        Assert.Null(session.TakeHint());

        Assert.True(session.Undo());
        Assert.Equal(1, session.TakeHint()!.Id);
        session.TryPlay(1);
        Assert.True(session.IsSolved);
        Assert.Equal(2, session.Stars);
    }

    [Fact]
    public void Solved_event_grows_negative_number_skill()
    {
        var puzzle = NumberHopGenerator.Generate(1, 4);
        var session = new NumberHopSession(puzzle);
        foreach (int id in NumberHopSolver.Solve(puzzle.Start, puzzle.Target, puzzle.Hand)!)
            session.TryPlay(id);

        var e = session.ToEvent(GameOutcome.Solved, Guid.NewGuid(), 15_000, DateTimeOffset.Now);
        var model = ProgressModel.Replay(new[] { e });

        Assert.Equal(NumberHopGame.SkillId, e.SkillId);
        Assert.Equal(2, LevelSelector.HighestUnlocked(NumberHopGame.Info, model));
    }
}
