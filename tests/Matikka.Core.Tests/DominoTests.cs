using Matikka.Core.Games;
using Matikka.Core.Games.EquationBalance;
using Matikka.Core.Games.EquationDomino;
using Matikka.Core.Progress;

namespace Matikka.Core.Tests;

public class DominoTests
{
    public static TheoryData<int> AllLevels()
    {
        var data = new TheoryData<int>();
        for (int level = 1; level <= DominoGenerator.MaxLevel; level++)
            data.Add(level);
        return data;
    }

    /// <summary>Pelaa ketjun loppuun valitsemalla aina oikean laatan.</summary>
    private static DominoSession PlayPerfectly(DominoPuzzle puzzle)
    {
        var session = new DominoSession(puzzle);
        while (!session.IsSolved)
        {
            int solution = session.OpenEquation!.Solution;
            DominoTile next = session.Hand.Single(t => t.Answer == solution);
            Assert.True(session.TryPlace(next.Id).Correct);
        }

        return session;
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Every_chain_has_exactly_one_fitting_tile_at_each_step(int level)
    {
        for (long seed = 0; seed < 1000; seed++)
        {
            DominoPuzzle puzzle = DominoGenerator.Generate(level, seed);
            var session = new DominoSession(puzzle);

            Assert.Equal(puzzle.Hand.Count, puzzle.Hand.Select(t => t.Answer).Distinct().Count());
            Assert.Single(puzzle.Hand, t => t.IsFinish);

            while (!session.IsSolved)
            {
                DominoEquation open = session.OpenEquation!;
                Assert.False(open.Expanded.IsSolved);
                Assert.True(open.Expanded.IsSatisfiedBy(open.Solution));
                Assert.Equal(open.LeftAt(open.Solution), open.RightAt(open.Solution));

                var fitting = session.Hand.Where(t => open.Expanded.IsSatisfiedBy(t.Answer!.Value)).ToList();
                Assert.Single(fitting);
                session.TryPlace(fitting[0].Id);
            }

            Assert.Null(session.OpenEquation);
            Assert.Equal(0, session.Mistakes);
            Assert.Equal(3, session.Stars);
        }
    }

    [Fact]
    public void Same_level_and_seed_give_the_same_game()
    {
        var a = DominoGenerator.Generate(5, 99);
        var b = DominoGenerator.Generate(5, 99);

        Assert.Equal(a.Start, b.Start);
        Assert.True(a.Hand.SequenceEqual(b.Hand));
    }

    [Fact]
    public void Hand_is_never_dealt_in_chain_order()
    {
        for (long seed = 0; seed < 500; seed++)
        {
            var puzzle = DominoGenerator.Generate(1, seed);
            Assert.False(puzzle.Hand.Select(t => t.Id).SequenceEqual(Enumerable.Range(1, puzzle.Hand.Count)));
        }
    }

    [Fact]
    public void Sign_trap_level_contains_opposite_answers()
    {
        for (long seed = 0; seed < 300; seed++)
        {
            var answers = DominoGenerator.Generate(4, seed).Hand.Select(t => t.Answer!.Value).ToList();
            Assert.True(answers.Count(a => answers.Contains(-a)) >= 6);
        }
    }

    [Fact]
    public void Bracket_level_shows_brackets_and_hint_divides_by_factor()
    {
        bool sawDivisionHint = false;
        for (long seed = 0; seed < 200; seed++)
        {
            var puzzle = DominoGenerator.Generate(6, seed);
            Assert.Contains("(x ", puzzle.Start.Equation!.Display);

            var session = new DominoSession(puzzle);
            if (puzzle.Start.Equation.BracketFactor is int a)
            {
                Assert.Equal(BalanceMove.DivideBy(a), session.TakeHint());
                sawDivisionHint = true;
            }
        }

        Assert.True(sawDivisionHint);
    }

    [Fact]
    public void Wrong_tile_is_explained_by_substitution_and_counted()
    {
        var puzzle = DominoGenerator.Generate(3, 7);
        var session = new DominoSession(puzzle);
        DominoEquation open = session.OpenEquation!;
        DominoTile wrong = session.Hand.First(t => t.Answer != open.Solution);

        PlaceResult result = session.TryPlace(wrong.Id);

        Assert.False(result.Correct);
        Assert.Equal(wrong.Answer, result.TriedAnswer);
        Assert.NotEqual(result.LeftValue, result.RightValue);
        Assert.Equal(1, session.Mistakes);
        Assert.Equal(0, session.Placed);
    }

    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(2, 0, 2)]
    [InlineData(3, 0, 1)]
    [InlineData(0, 1, 2)]
    public void Stars_follow_mistakes_and_hints(int mistakes, int hints, int expected)
    {
        Assert.Equal(expected, DominoSession.CalculateStars(mistakes, hints));
    }

    [Fact]
    public void Domino_and_balance_grow_the_same_skill()
    {
        var session = PlayPerfectly(DominoGenerator.Generate(1, 1));
        var e = session.ToEvent(GameOutcome.Solved, Guid.NewGuid(), 30_000, DateTimeOffset.Now);

        var model = ProgressModel.Replay(new[] { e });

        Assert.Equal(EquationDominoGame.GameId, e.GameId);
        Assert.Equal(e.Par, e.Moves);
        Assert.True(model.GetSkill(EquationBalanceGame.Info).Rating > EquationBalanceGame.Info.InitialRating);
        Assert.Equal(3, model.GetLevel(EquationDominoGame.GameId, 1).BestStars);
        Assert.Equal(2, LevelSelector_HighestUnlocked(EquationDominoGame.Info, model));
        Assert.Equal(1, LevelSelector_HighestUnlocked(EquationBalanceGame.Info, model));
    }

    private static int LevelSelector_HighestUnlocked(GameInfo game, ProgressModel model) =>
        Adaptivity.LevelSelector.HighestUnlocked(game, model);
}
