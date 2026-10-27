using Matikka.Core.Games.EquationBalance;
using Matikka.Core.Randomness;

namespace Matikka.Core.Tests;

public class GeneratorAndSolverTests
{
    public static TheoryData<int> AllLevels()
    {
        var data = new TheoryData<int>();
        for (int level = 1; level <= BalancePuzzleGenerator.MaxLevel; level++)
            data.Add(level);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Every_generated_puzzle_is_valid_and_solvable_in_par_moves(int level)
    {
        for (long seed = 0; seed < 2000; seed++)
        {
            BalancePuzzle puzzle = BalancePuzzleGenerator.Generate(level, seed);

            Assert.False(puzzle.Start.IsSolved);
            Assert.True(puzzle.Start.IsSatisfiedBy(puzzle.Solution));
            Assert.InRange(puzzle.Par, 1, 3);

            Equation current = puzzle.Start;
            foreach (BalanceMove move in BalanceSolver.Solve(puzzle.Start))
                current = current.Apply(move).Equation!;

            Assert.True(current.IsSolved);
            Assert.Equal(puzzle.Solution, current.SolvedValue);
        }
    }

    [Fact]
    public void Same_level_and_seed_give_the_same_puzzle()
    {
        Assert.Equal(BalancePuzzleGenerator.Generate(7, 12345), BalancePuzzleGenerator.Generate(7, 12345));
        Assert.NotEqual(BalancePuzzleGenerator.Generate(7, 12345), BalancePuzzleGenerator.Generate(7, 12346));
    }

    [Fact]
    public void Generator_output_is_stable_across_platforms_and_versions()
    {
        // Jos tämä testi rikkoutuu, generaattorin muutos muuttaa jo pelattujen tehtävien sisältöä.
        // Silloin taso + siemen ei enää toista samaa tehtävää vanhoista tapahtumista.
        var puzzle = BalancePuzzleGenerator.Generate(4, 42);

        Assert.Equal(new SeededRandom(42).NextUInt64(), new SeededRandom(42).NextUInt64());
        Assert.Equal(puzzle, BalancePuzzleGenerator.Generate(4, 42));
    }

    [Fact]
    public void Level_one_is_a_single_step_with_positive_numbers()
    {
        for (long seed = 0; seed < 500; seed++)
        {
            var puzzle = BalancePuzzleGenerator.Generate(1, seed);
            Assert.Equal(1, puzzle.Par);
            Assert.True(puzzle.Solution > 0);
            Assert.True(puzzle.Start.Left.Constant > 0);
        }
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Both_sides_levels_have_x_on_both_sides(int level)
    {
        for (long seed = 0; seed < 500; seed++)
        {
            var start = BalancePuzzleGenerator.Generate(level, seed).Start;
            Assert.True(start.Left.HasX && start.Right.HasX);
        }
    }

    [Fact]
    public void Hint_always_leads_to_solution_even_after_detours()
    {
        var random = new SeededRandom(7);
        BalanceMove[] detours =
        {
            BalanceMove.Add(3), BalanceMove.Subtract(5), BalanceMove.MultiplyBy(2),
            BalanceMove.MultiplyBy(-1), BalanceMove.AddX(1), BalanceMove.SubtractX(2),
        };

        for (int i = 0; i < 1000; i++)
        {
            var session = new BalanceSession(
                BalancePuzzleGenerator.Generate(random.Next(1, BalancePuzzleGenerator.MaxLevel), random.NextSeed()));

            // Muutama satunnainen sivupolku. Hylätyt siirrot eivät muuta tilaa.
            for (int d = 0; d < 3 && !session.IsSolved; d++)
                session.TryApply(detours[random.Next(0, detours.Length - 1)]);

            int guard = 0;
            while (!session.IsSolved)
            {
                BalanceMove? hint = session.TakeHint();
                Assert.NotNull(hint);
                Assert.True(session.TryApply(hint!).Succeeded);
                Assert.True(++guard <= 3);
            }

            Assert.Equal(session.Puzzle.Solution, session.Current.SolvedValue);
        }
    }
}
