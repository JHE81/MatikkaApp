using Matikka.Core.Games.EquationBalance;

namespace Matikka.Core.Tests;

public class EquationTests
{
    [Fact]
    public void Subtracting_constant_keeps_balance()
    {
        var eq = Equation.Of(2, 4, 0, 10); // 2x + 4 = 10

        MoveResult result = eq.Apply(BalanceMove.Subtract(4));

        Assert.True(result.Succeeded);
        Assert.Equal(Equation.Of(2, 0, 0, 6), result.Equation);
    }

    [Fact]
    public void Subtracting_x_terms_moves_x_to_one_side()
    {
        var eq = Equation.Of(5, 3, 2, 12); // 5x + 3 = 2x + 12

        MoveResult result = eq.Apply(BalanceMove.SubtractX(2));

        Assert.Equal(Equation.Of(3, 3, 0, 12), result.Equation);
    }

    [Fact]
    public void Division_requires_every_term_to_divide_evenly()
    {
        var eq = Equation.Of(3, 0, 0, 7); // 3x = 7

        MoveResult result = eq.Apply(BalanceMove.DivideBy(3));

        Assert.False(result.Succeeded);
        Assert.Equal(MoveError.NotDivisible, result.Error);
    }

    [Fact]
    public void Division_by_negative_coefficient_solves()
    {
        var eq = Equation.Of(-4, 0, 0, 12);

        MoveResult result = eq.Apply(BalanceMove.DivideBy(-4));

        Assert.True(result.Equation!.IsSolved);
        Assert.Equal(-3, result.Equation.SolvedValue);
    }

    [Theory]
    [InlineData(Operation.Multiply, 0, MoveError.MultiplyByZero)]
    [InlineData(Operation.Divide, 0, MoveError.DivideByZero)]
    [InlineData(Operation.Add, 0, MoveError.NoEffect)]
    [InlineData(Operation.Multiply, 1, MoveError.NoEffect)]
    public void Pointless_or_invalid_moves_are_rejected(Operation op, int value, MoveError expected)
    {
        var eq = Equation.Of(2, 4, 0, 10);

        MoveResult result = eq.Apply(new BalanceMove(op, value));

        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public void Multiplying_with_x_term_is_not_allowed()
    {
        var eq = Equation.Of(2, 4, 0, 10);

        Assert.Equal(MoveError.XTermNotAllowed, eq.Apply(new BalanceMove(Operation.Multiply, 2, IsXTerm: true)).Error);
    }

    [Fact]
    public void Numbers_cannot_grow_without_limit()
    {
        var eq = Equation.Of(2, 400, 0, 600);

        Assert.Equal(MoveError.NumbersTooLarge, eq.Apply(BalanceMove.MultiplyBy(3)).Error);
    }

    [Theory]
    [InlineData(1, 0, 0, 5, true)]   // x = 5
    [InlineData(0, -2, 1, 0, true)]  // -2 = x
    [InlineData(2, 0, 0, 6, false)]  // 2x = 6
    [InlineData(1, 1, 0, 6, false)]  // x + 1 = 6
    [InlineData(1, 0, 1, 2, false)]  // x = x + 2
    public void Solved_means_x_alone_on_one_side(int a, int b, int c, int d, bool solved)
    {
        Assert.Equal(solved, Equation.Of(a, b, c, d).IsSolved);
    }

    [Theory]
    [InlineData(3, 4, 0, 10, "3x + 4 = 10")]
    [InlineData(-1, -2, 1, 0, "-x - 2 = x")]
    [InlineData(1, 0, -5, 7, "x = -5x + 7")]
    [InlineData(0, 0, 2, 0, "0 = 2x")]
    public void Formats_like_a_textbook(int a, int b, int c, int d, string expected)
    {
        Assert.Equal(expected, Equation.Of(a, b, c, d).ToString());
    }
}
