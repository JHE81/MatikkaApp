using Matikka.Core.Adaptivity;
using Matikka.Core.Games.EquationBalance;
using Matikka.Core.Progress;
using Matikka.Core.Randomness;

namespace Matikka.Core.Tests;

public class ProgressTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 12, 0, 0, TimeSpan.FromHours(3));

    private static GameEvent Solved(int level, int stars, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        GameId = EquationBalanceGame.GameId,
        SkillId = EquationBalanceGame.SkillId,
        Level = level,
        Outcome = GameOutcome.Solved,
        Stars = stars,
        OccurredAt = at,
    };

    [Fact]
    public void New_player_starts_on_level_one_at_target_success()
    {
        var progress = new ProgressModel();
        var skill = progress.GetSkill(EquationBalanceGame.Info);

        double p = EloRating.Expected(skill.Rating, EquationBalanceGame.Info.GetLevel(1).Difficulty);

        Assert.InRange(p, 0.749, 0.751);
        Assert.Equal(1, LevelSelector.ChooseNext(EquationBalanceGame.Info, progress, new SeededRandom(1)));
    }

    [Fact]
    public void Success_raises_rating_and_failure_lowers_it()
    {
        Assert.True(EloRating.Update(0, 0, 1.0, 0) > 0);
        Assert.True(EloRating.Update(0, 0, 0.0, 0) < 0);
        Assert.Equal(0, EloRating.Update(0, 0, 0.5, 0), 10);
    }

    [Fact]
    public void Replay_is_independent_of_arrival_order()
    {
        var events = Enumerable.Range(0, 40)
            .Select(i => Solved(1 + i % 4, 1 + i % 3, T0.AddMinutes(i)))
            .ToList();

        var a = ProgressModel.Replay(events);
        var b = ProgressModel.Replay(events.AsEnumerable().Reverse());

        Assert.Equal(a.GetSkill(EquationBalanceGame.Info).Rating, b.GetSkill(EquationBalanceGame.Info).Rating);
        Assert.Equal(a.TotalStars, b.TotalStars);
    }

    [Fact]
    public void Duplicate_events_are_counted_once()
    {
        var e = Solved(1, 3, T0);
        var model = ProgressModel.Replay(new[] { e, e });

        Assert.Equal(1, model.TotalSolved);
        Assert.True(model.TryAppend(e));
        Assert.Equal(1, model.TotalSolved);
    }

    [Fact]
    public void Out_of_order_append_asks_for_replay()
    {
        var model = new ProgressModel();
        Assert.True(model.TryAppend(Solved(1, 3, T0.AddHours(1))));

        Assert.False(model.TryAppend(Solved(1, 3, T0)));
    }

    [Fact]
    public void Levels_unlock_one_at_a_time()
    {
        var game = EquationBalanceGame.Info;
        var model = new ProgressModel();
        Assert.Equal(1, LevelSelector.HighestUnlocked(game, model));

        model.TryAppend(Solved(1, 1, T0));
        Assert.Equal(2, LevelSelector.HighestUnlocked(game, model));
        Assert.False(LevelSelector.IsUnlocked(game, model, 3));
    }

    [Fact]
    public void Strong_player_is_offered_harder_levels()
    {
        var game = EquationBalanceGame.Info;
        var events = new List<GameEvent>();
        for (int i = 0; i < 60; i++)
            events.Add(Solved(1 + i / 8 % 8, 3, T0.AddMinutes(i)));

        var model = ProgressModel.Replay(events);
        var random = new SeededRandom(3);
        var chosen = Enumerable.Range(0, 200).Select(_ => LevelSelector.ChooseNext(game, model, random)).ToList();

        Assert.True(chosen.Average() > 5);
        Assert.Contains(chosen, l => l < chosen.Max()); // kertaustehtäviä tulee myös
    }

    [Fact]
    public void Streak_counts_consecutive_days()
    {
        var model = ProgressModel.Replay(new[]
        {
            Solved(1, 3, T0),
            Solved(1, 3, T0.AddDays(1)),
            Solved(1, 3, T0.AddDays(2)),
            Solved(1, 3, T0.AddDays(5)),
        });

        Assert.Equal(1, model.CurrentStreak(T0.AddDays(5).Date));
        Assert.Equal(3, model.CurrentStreak(T0.AddDays(3).Date)); // eilen pelattu riittää
        Assert.Equal(0, model.CurrentStreak(T0.AddDays(10).Date));
    }
}
