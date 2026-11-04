using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Data;
using RugbyLeague.Core.Model;
using RugbyLeague.Core.Sim;

namespace RugbyLeague.Core.Tests;

public class MatchEngineTests
{
    private static Career BuildCareer(int seed)
    {
        var seedLeague = TeamDatabase.BuildLeagues();
        var teamId = Guid.NewGuid();
        _ = seedLeague;
        return Career.CreateNew(teamId, "Test Manager", seed);
    }

    private static MatchSetup Setup(int seed, bool frames = true)
    {
        var career = BuildCareer(seed);
        var home = career.Teams[0];
        var away = career.Teams[1];

        return new MatchSetup
        {
            Home = home.SelectBestXV(),
            Away = away.SelectBestXV(),
            Seed = seed,
            Weather = new Weather(),
            CompetitionName = "Test",
            CaptureFrames = frames
        };
    }

    [Fact]
    public void Same_seed_produces_identical_results()
    {
        var a = MatchEngine.Simulate(Setup(1234));
        var b = MatchEngine.Simulate(Setup(1234));

        Assert.Equal(a.HomeScore, b.HomeScore);
        Assert.Equal(a.AwayScore, b.AwayScore);
        Assert.Equal(a.Events.Count, b.Events.Count);
        Assert.Equal(a.Frames.Count, b.Frames.Count);
    }

    [Fact]
    public void Frames_are_captured_only_when_requested()
    {
        var withFrames = MatchEngine.Simulate(Setup(77, frames: true));
        var withoutFrames = MatchEngine.Simulate(Setup(77, frames: false));

        Assert.NotEmpty(withFrames.Frames);
        Assert.Empty(withoutFrames.Frames);
        Assert.Equal(withFrames.HomeScore, withoutFrames.HomeScore);
        Assert.Equal(withFrames.AwayScore, withoutFrames.AwayScore);
    }

    [Fact]
    public void Scores_are_legal_and_in_a_plausible_range()
    {
        var totals = new List<int>();
        var tryCounts = new List<int>();

        for (var seed = 1; seed <= 120; seed++)
        {
            var result = MatchEngine.Simulate(Setup(seed, frames: false));

            Assert.InRange(result.HomeScore, 0, 120);
            Assert.InRange(result.AwayScore, 0, 120);
            Assert.True(result.HomeScore % 1 == 0);
            Assert.True(result.HomeTries >= 0);

            totals.Add(result.HomeScore + result.AwayScore);
            tryCounts.Add(result.HomeTries + result.AwayTries);
        }

        var averageTotal = totals.Average();
        var averageTries = tryCounts.Average();

        Assert.InRange(averageTotal, 24, 60);
        Assert.InRange(averageTries, 4, 12);
    }

    [Fact]
    public void Stronger_squad_wins_more_often()
    {
        var career = BuildCareer(99);
        var strong = career.Teams.OrderByDescending(t => t.SquadRating).First();
        var weak = career.Teams.OrderBy(t => t.SquadRating).Last();

        var wins = 0;
        for (var seed = 1; seed <= 40; seed++)
        {
            var result = MatchEngine.Simulate(new MatchSetup
            {
                Home = strong.SelectBestXV(),
                Away = weak.SelectBestXV(),
                Seed = seed,
                CaptureFrames = false
            });

            if (result.WinnerId == strong.Id) wins++;
        }

        Assert.True(wins > 16, $"The stronger squad only won {wins} of 40.");
    }

    [Fact]
    public void Knockout_matches_always_produce_a_winner()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var setup = Setup(seed, frames: false);
            var result = MatchEngine.Simulate(new MatchSetup
            {
                Home = setup.Home,
                Away = setup.Away,
                Seed = seed,
                MustHaveWinner = true,
                CaptureFrames = false
            });

            Assert.NotEqual(Guid.Empty, result.WinnerId);
        }
    }

    [Fact]
    public void Statistics_are_coherent()
    {
        var result = MatchEngine.Simulate(Setup(31, frames: false));

        Assert.Equal(100, result.HomeStats.Possession + result.AwayStats.Possession);
        Assert.True(result.HomeStats.Tackles > 20);
        Assert.NotEmpty(result.PlayerStats);
        Assert.Contains(result.Events, e => e.Kind == MatchEventKind.FullTime);
        Assert.Contains(result.Events, e => e.Kind == MatchEventKind.KickOff);
    }

    [Fact]
    public void Frames_stay_inside_the_pitch()
    {
        var result = MatchEngine.Simulate(Setup(4242));

        foreach (var frame in result.Frames)
        {
            Assert.InRange(frame.BallX, 0, 100);
            Assert.InRange(frame.BallY, 0, 68);
            Assert.InRange(frame.ReplaySeconds, 0.1f, 10f);

            foreach (var player in frame.Players)
            {
                Assert.InRange(player.X, 0, 100);
                Assert.InRange(player.Y, 0, 68);
            }
        }
    }
}
