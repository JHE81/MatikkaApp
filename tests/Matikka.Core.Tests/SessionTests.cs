using Matikka.Core.Games.EquationBalance;

namespace Matikka.Core.Tests;

public class SessionTests
{
    private static BalancePuzzle Puzzle(Equation start)
    {
        var moves = BalanceSolver.Solve(start);
        Equation solved = start;
        foreach (var m in moves)
            solved = solved.Apply(m).Equation!;
        return new BalancePuzzle(4, 1, start, solved.SolvedValue!.Value, moves.Count);
    }

    [Fact]
    public void Solving_in_par_gives_three_stars()
    {
        var session = new BalanceSession(Puzzle(Equation.Of(2, 4, 0, 10)));

        session.TryApply(BalanceMove.Subtract(4));
        session.TryApply(BalanceMove.DivideBy(2));

        Assert.True(session.IsSolved);
        Assert.Equal(3, session.Stars);
    }

    [Fact]
    public void Undo_restores_previous_state_and_move_count()
    {
        var start = Equation.Of(2, 4, 0, 10);
        var session = new BalanceSession(Puzzle(start));

        session.TryApply(BalanceMove.Add(1));
        Assert.True(session.Undo());

        Assert.Equal(start, session.Current);
        Assert.Equal(0, session.MoveCount);
        Assert.False(session.Undo());
    }

    [Fact]
    public void Rejected_move_does_not_count()
    {
        var session = new BalanceSession(Puzzle(Equation.Of(3, 1, 0, 7)));

        var result = session.TryApply(BalanceMove.DivideBy(3));

        Assert.False(result.Succeeded);
        Assert.Equal(0, session.MoveCount);
    }

    [Theory]
    [InlineData(2, 2, 0, 3)]
    [InlineData(4, 2, 0, 2)]
    [InlineData(5, 2, 0, 1)]
    [InlineData(2, 2, 1, 2)]
    [InlineData(6, 2, 1, 1)]
    public void Stars_follow_moves_par_and_hints(int moves, int par, int hints, int expected)
    {
        Assert.Equal(expected, BalanceSession.CalculateStars(moves, par, hints));
    }
}
