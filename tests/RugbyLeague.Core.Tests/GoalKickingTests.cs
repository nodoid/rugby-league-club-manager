using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Data;
using RugbyLeague.Core.Model;
using RugbyLeague.Core.Sim;

namespace RugbyLeague.Core.Tests;

public class GoalKickingTests
{
    [Fact]
    public void Required_power_rises_with_distance()
    {
        Assert.True(GoalKicking.RequiredPower(40) > GoalKicking.RequiredPower(20));
        Assert.InRange(GoalKicking.RequiredPower(100), 0.34f, 0.99f);
    }

    [Fact]
    public void A_perfect_strike_from_close_range_goes_over()
    {
        var power = GoalKicking.RequiredPower(15);
        var outcome = GoalKicking.Evaluate(new GoalKicking.Attempt(15, power, 0, 90, 90, 0));

        Assert.True(outcome.Scored);
        Assert.False(outcome.Short);
        Assert.False(outcome.PowerError > 0.01f);
    }

    [Fact]
    public void Too_little_power_falls_short()
    {
        var outcome = GoalKicking.Evaluate(new GoalKicking.Attempt(40, 0.2f, 0, 80, 80, 0));

        Assert.True(outcome.Short);
        Assert.False(outcome.Scored);
    }

    [Fact]
    public void A_cross_wind_pushes_the_ball_off_line()
    {
        var power = GoalKicking.RequiredPower(30);
        var still = GoalKicking.Evaluate(new GoalKicking.Attempt(30, power, 0, 80, 80, 0));
        var wind = GoalKicking.Evaluate(new GoalKicking.Attempt(30, power, 20, 80, 80, 0));

        Assert.True(MathF.Abs(wind.LateralMiss) > MathF.Abs(still.LateralMiss));
        Assert.True(wind.LateralMiss > 0);
    }

    [Fact]
    public void A_headwind_lengthens_the_kick_and_a_tailwind_shortens_it()
    {
        var power = GoalKicking.RequiredPower(30);
        var still = GoalKicking.Evaluate(new GoalKicking.Attempt(30, power, 0, 80, 80, 0));
        var intoWind = GoalKicking.Evaluate(new GoalKicking.Attempt(30, power, 0, 80, 80, 0, -40));
        var downwind = GoalKicking.Evaluate(new GoalKicking.Attempt(30, power, 0, 80, 80, 0, 40));

        Assert.True(GoalKicking.EffectiveDistance(30, -40) > 30);
        Assert.True(GoalKicking.EffectiveDistance(30, 40) < 30);
        Assert.True(intoWind.PowerError < still.PowerError);
        Assert.True(downwind.PowerError > still.PowerError);
    }

    [Fact]
    public void Kicking_training_improves_the_conversion_rate()
    {
        var career = Career.CreateNew(Guid.NewGuid(), "Test Manager", 5150);
        // The session sharpens the club's two best kickers, who are not always in the starting XIII.
        int BestKickers() => career.Teams[0].Squad
            .OrderByDescending(p => p.Attributes.GoalKicking)
            .Take(2)
            .Sum(p => p.Attributes.GoalKicking);

        var before = BestKickers();

        var (success, message) = career.RunKickingSession();
        var after = BestKickers();

        Assert.True(success, message);
        Assert.True(after > before, $"Goal kicking went from {before} to {after}.");
    }

    [Fact]
    public void Wind_is_deterministic_for_a_round()
    {
        var career = Career.CreateNew(Guid.NewGuid(), "Test Manager", 4242);
        var first = career.WeatherForRound(3);
        var second = career.WeatherForRound(3);

        Assert.Equal(first.WindSpeed, second.WindSpeed);
        Assert.Equal(first.WindBearing, second.WindBearing);
        Assert.Equal(first.WindAcross, second.WindAcross);
    }

    [Fact]
    public void A_north_wind_blows_along_the_pitch_and_east_across_it()
    {
        var north = new Weather { WindSpeed = 20, WindBearing = 0 };
        Assert.Equal(0, north.WindAcross, 3);
        Assert.True(north.WindAlong > 19);

        var east = new Weather { WindSpeed = 20, WindBearing = 90 };
        Assert.True(east.WindAcross > 19);
        Assert.Equal(0, east.WindAlong, 3);
    }

    [Fact]
    public void Head_assessments_are_recorded_in_simulated_matches()
    {
        var career = Career.CreateNew(Guid.NewGuid(), "Test Manager", 1234);
        var home = career.Teams[0];
        var away = career.Teams[1];

        var assessments = 0;
        for (var seed = 1; seed <= 60; seed++)
        {
            var result = MatchEngine.Simulate(new MatchSetup
            {
                Home = home.SelectBestXV(),
                Away = away.SelectBestXV(),
                Seed = seed,
                CaptureFrames = false
            });

            assessments += result.Events.Count(e => e.Kind == MatchEventKind.HeadAssessment);
        }

        Assert.True(assessments > 0, "No head injury assessments in sixty matches.");
    }
}
