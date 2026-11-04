using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Model;

namespace RugbyLeague.Core.Tests;

public class LeagueAndCupTests
{
    [Fact]
    public void Double_round_robin_never_double_books_a_team()
    {
        var ids = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToList();
        var fixtures = CompetitionFactory.GenerateDoubleRoundRobin(ids, new Random(1));

        Assert.Equal(22, fixtures.Max(f => f.Round) + 1);

        foreach (var group in fixtures.GroupBy(f => f.Round))
        {
            var playing = group.Where(f => !f.IsBye).SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).ToList();
            Assert.Equal(playing.Count, playing.Distinct().Count());
            Assert.Equal(6, group.Count(f => !f.IsBye));
        }
    }

    [Fact]
    public void Odd_leagues_receive_a_bye_each_round()
    {
        var ids = Enumerable.Range(0, 17).Select(_ => Guid.NewGuid()).ToList();
        var fixtures = CompetitionFactory.GenerateDoubleRoundRobin(ids, new Random(2), 22);

        var rounds = fixtures.GroupBy(f => f.Round).ToList();
        Assert.Equal(22, rounds.Count);
        Assert.All(rounds, r => Assert.Single(r.Where(f => f.IsBye)));
    }

    [Fact]
    public void Table_awards_points_and_orders_by_difference()
    {
        var league = new League { Id = Guid.NewGuid(), PointsForWin = 2, PointsForDraw = 1 };
        var a = new Team { Id = Guid.NewGuid(), Name = "A" };
        var b = new Team { Id = Guid.NewGuid(), Name = "B" };
        league.TeamIds.AddRange(new[] { a.Id, b.Id });

        var teams = new Dictionary<Guid, Team> { [a.Id] = a, [b.Id] = b };
        league.Fixtures.Add(new Fixture
        {
            Round = 0,
            HomeTeamId = a.Id,
            AwayTeamId = b.Id,
            Result = new MatchResult { HomeTeamId = a.Id, AwayTeamId = b.Id, HomeScore = 24, AwayScore = 10 }
        });

        var table = league.BuildTable(teams);

        Assert.Equal(a.Id, table[0].TeamId);
        Assert.Equal(2, table[0].Points);
        Assert.Equal(14, table[0].Difference);
        Assert.Equal(0, table[1].Points);
        Assert.Equal("W", table[0].Form.Single());
    }

    [Fact]
    public void Byes_award_points_without_a_match()
    {
        var league = new League { ByePoints = 2 };
        var a = new Team { Id = Guid.NewGuid(), Name = "A" };
        league.TeamIds.Add(a.Id);
        league.Fixtures.Add(new Fixture { Round = 0, HomeTeamId = a.Id, AwayTeamId = Guid.Empty });

        var table = league.BuildTable(new Dictionary<Guid, Team> { [a.Id] = a });

        Assert.Equal(2, table[0].Points);
        Assert.Equal(1, table[0].Byes);
        Assert.Equal(0, table[0].Played);
    }

    [Fact]
    public void Cup_bracket_pads_with_byes_and_crowns_a_winner()
    {
        var seeds = Enumerable.Range(0, 41).Select(_ => Guid.NewGuid()).ToList();
        var cup = CompetitionFactory.CreateKnockout("Cup", "CUP", seeds, 10_000, Guid.NewGuid());

        Assert.Equal(6, cup.Rounds.Count);
        Assert.Equal(23, cup.Rounds[0].Byes.Count);
        Assert.Equal(9, cup.Rounds[0].Fixtures.Count);

        var roundIndex = 0;
        while (!cup.IsComplete && roundIndex++ < 10)
        {
            var round = cup.CurrentRound!;
            var winners = new List<Guid>();

            foreach (var fixture in round.Fixtures)
            {
                fixture.Result = new MatchResult
                {
                    HomeTeamId = fixture.HomeTeamId,
                    AwayTeamId = fixture.AwayTeamId,
                    HomeScore = 20,
                    AwayScore = 12
                };
                winners.Add(fixture.HomeTeamId);
            }

            CompetitionFactory.Advance(cup, winners);
        }

        Assert.True(cup.IsComplete);
        Assert.NotNull(cup.WinnerId);
        Assert.Contains(cup.WinnerId!.Value, seeds);
    }

    [Fact]
    public void Cup_re_seeds_the_best_remaining_team_against_the_worst()
    {
        var seeds = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToList();
        var cup = CompetitionFactory.CreateKnockout("Cup", "CUP", seeds, 1, Guid.Empty);

        // Favourites (lower seed index) all win.
        var winners = cup.Rounds[0].Fixtures.Select(f => f.HomeTeamId).ToList();
        CompetitionFactory.Advance(cup, winners);

        var next = cup.CurrentRound!;
        Assert.Contains(next.Fixtures, f => f.HomeTeamId == seeds[0] && f.AwayTeamId == seeds[3]);
        Assert.Contains(next.Fixtures, f => f.HomeTeamId == seeds[1] && f.AwayTeamId == seeds[2]);
    }

    [Fact]
    public void Six_team_finals_give_the_top_two_a_bye()
    {
        var seeds = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();
        var finals = CompetitionFactory.CreateKnockout("Finals", "FNL", seeds, 1, Guid.Empty, isFinals: true);

        Assert.Equal(2, finals.Rounds[0].Byes.Count);
        Assert.Equal(2, finals.Rounds[0].Fixtures.Count);
        Assert.Equal("Grand Final", finals.Rounds[^1].Name);
        Assert.Equal("Semi-Finals", finals.Rounds[1].Name);
    }
}
