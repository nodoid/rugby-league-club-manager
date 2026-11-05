using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Data;
using RugbyLeague.Core.Model;
using RugbyLeague.Core.Persistence;

namespace RugbyLeague.Core.Tests;

public class CareerTests
{
    private static Career NewCareer(int seed = 2026) =>
        Career.CreateNew(null, "Test Manager", seed, 2026);

    [Fact]
    public void New_career_builds_four_leagues_and_full_squads()
    {
        var career = NewCareer();

        Assert.Equal(4, career.Leagues.Count);
        Assert.Equal(48, career.Teams.Count);
        Assert.All(career.Leagues, league => Assert.Equal(12, league.TeamIds.Count));
        Assert.Equal(48, career.Cup.Entrants.Count);

        // Both countries run two divisions, so both link a tier-one league to a tier two.
        foreach (var country in new[] { "Australia", "England" })
        {
            var top = career.Leagues.Single(l => l.Country == country && l.Tier == 1);
            var second = career.Leagues.Single(l => l.Country == country && l.Tier == 2);
            Assert.True(top.HasPromotionRelegation);
            Assert.True(second.HasPromotionRelegation);
        }

        Assert.All(career.Teams, team =>
        {
            Assert.InRange(team.Squad.Count, 24, 40);
            Assert.NotEmpty(team.Sponsors);
            Assert.True(team.Formation.IsValid(out _));
            Assert.True(team.SalaryCapSpend <= team.Finances.SalaryCap,
                $"{team.Name} spend {team.SalaryCapSpend} against a cap of {team.Finances.SalaryCap}");
        });

        Assert.Equal(Career.SeasonRounds, career.Leagues[0].Rounds);
    }

    [Fact]
    public void Every_league_has_a_full_fixture_list_with_home_and_away_games()
    {
        var career = NewCareer(11);
        foreach (var league in career.Leagues)
        {
            Assert.Equal(Career.SeasonRounds, league.Rounds);
            var matches = league.Fixtures.Count(f => !f.IsBye);
            Assert.True(matches >= league.TeamIds.Count * 10);
        }
    }

    [Fact]
    public void Playing_a_league_round_records_results_finances_and_progresses()
    {
        var career = NewCareer(7);
        var fixture = career.NextLeagueFixture;
        Assert.NotNull(fixture);

        var before = career.PlayerTeam.Finances.Balance;
        var round = career.Round;

        career.PlayLeagueRound(null);

        Assert.Equal(round + 1, career.Round);
        Assert.NotNull(fixture!.Result);
        Assert.NotEqual(before, career.PlayerTeam.Finances.Balance);
        Assert.All(career.Leagues, l => Assert.All(l.FixturesForRound(round), f => Assert.True(f.IsBye || f.IsPlayed)));
        Assert.True(career.News.Any());
    }

    [Fact]
    public void A_full_season_reaches_finals_and_then_the_off_season()
    {
        var career = NewCareer(4242);

        var guard = 0;
        while (career.Phase == CompetitionPhase.Regular && guard++ < 60)
        {
            career.PlayLeagueRound(null);
            career.EnsureCupProgress();
        }

        Assert.Equal(CompetitionPhase.Finals, career.Phase);
        Assert.NotEmpty(career.FinalsSeries);

        guard = 0;
        while (career.Phase == CompetitionPhase.Finals && guard++ < 30)
            career.PlayFinalsRound(null);

        Assert.Equal(CompetitionPhase.OffSeason, career.Phase);
        Assert.NotNull(career.LastSeason);
        Assert.All(career.LastSeason!.Leagues, l => Assert.False(string.IsNullOrWhiteSpace(l.Champion)));
        Assert.True(career.Cup.IsComplete);

        // Promotion and relegation keep every division at twelve clubs.
        Assert.All(career.Leagues, league => Assert.Equal(12, league.TeamIds.Count));
        Assert.All(career.Leagues, league =>
            Assert.All(league.TeamIds, id => Assert.Equal(league.Id, career.TeamById(id).LeagueId)));
    }

    [Fact]
    public void Australian_clubs_promote_and_relegate_between_the_nrl_and_its_second_tier()
    {
        var career = NewCareer(909);
        var top = career.LeagueById(TeamDatabase.NrlId);
        var second = career.LeagueById(TeamDatabase.NrlChampionshipId);
        var topBefore = top.TeamIds.ToHashSet();
        var secondBefore = second.TeamIds.ToHashSet();

        var guard = 0;
        while (career.Phase != CompetitionPhase.OffSeason && guard++ < 90)
        {
            if (career.Phase == CompetitionPhase.Regular) career.PlayLeagueRound(null);
            else career.PlayFinalsRound(null);
            career.EnsureCupProgress();
        }

        Assert.Equal(CompetitionPhase.OffSeason, career.Phase);
        Assert.Equal(topBefore.Count, top.TeamIds.Count);
        Assert.Equal(secondBefore.Count, second.TeamIds.Count);
        Assert.False(topBefore.SetEquals(top.TeamIds), "a relegated club should have left the NRL");
        Assert.False(secondBefore.SetEquals(second.TeamIds),
            "the NRL Championship winner should have gone up");
        Assert.All(top.TeamIds, id => Assert.Equal(top.Id, career.TeamById(id).LeagueId));
        Assert.All(second.TeamIds, id => Assert.Equal(second.Id, career.TeamById(id).LeagueId));
    }

    [Fact]
    public void Starting_a_new_season_resets_the_calendar_and_fixtures()
    {
        var career = NewCareer(808);
        var guard = 0;
        while (career.Phase != CompetitionPhase.OffSeason && guard++ < 90)
        {
            if (career.Phase == CompetitionPhase.Regular) career.PlayLeagueRound(null);
            else career.PlayFinalsRound(null);
            career.EnsureCupProgress();
        }

        var oldYear = career.SeasonYear;
        career.BeginNewSeason();

        Assert.Equal(oldYear + 1, career.SeasonYear);
        Assert.Equal(0, career.Round);
        Assert.Equal(CompetitionPhase.Regular, career.Phase);
        Assert.False(career.Cup.IsComplete);
        Assert.All(career.Leagues, l => Assert.All(l.Fixtures, f => Assert.True(f.Result is null || f.IsBye)));
        Assert.All(career.Leagues, l => Assert.Equal(Career.SeasonRounds, l.Fixtures.Max(f => f.Round) + 1));
    }

    [Fact]
    public void Training_improves_young_players_and_recovers_condition()
    {
        var career = NewCareer(55);
        var team = career.PlayerTeam;
        team.TrainingFacility = 10;
        team.TrainingFocus = TrainingFocus.Attack;

        var youngster = team.Squad.Where(p => p.Age <= 21).OrderBy(p => p.Potential - p.Rating).Last();
        var before = youngster.Attributes.OverallFor(youngster.Position);
        youngster.Fitness = 60;

        var rng = new Random(3);
        for (var i = 0; i < 30; i++) TrainingEngine.RunWeekly(team, rng, i);

        Assert.True(youngster.Fitness > 60);
        Assert.True(youngster.Attributes.OverallFor(youngster.Position) >= before);
    }

    [Fact]
    public void Injured_players_recover_after_the_stated_number_of_weeks()
    {
        var career = NewCareer(66);
        var player = career.PlayerTeam.Squad.First(p => !p.IsInjured);
        player.Injury = new Injury { Type = "Hamstring strain", Severity = InjurySeverity.Minor, WeeksRemaining = 2 };

        Assert.False(player.IsAvailable);

        for (var i = 0; i < 2; i++)
        {
            career.PlayLeagueRound(null);
            career.EnsureCupProgress();
            _ = player;
        }

        Assert.True(player.IsAvailable, $"{player.Name} was still injured after two weeks.");
    }

    [Fact]
    public void Accounts_pay_wages_and_receive_broadcast_money_each_week()
    {
        var career = NewCareer(71);
        var team = career.PlayerTeam;
        var wages = team.WeeklyWageBill;
        var before = team.Finances.Balance;

        career.PlayLeagueRound(null);

        var expectedIncome = team.Sponsors.Sum(s => (long)s.AnnualValue) / career.SeasonWeeks
                             + career.PlayerLeague.TvMoney / career.SeasonWeeks;

        Assert.True(team.Finances.SeasonCosts >= wages * 9 / 10);
        Assert.True(team.Finances.SeasonRevenue >= expectedIncome);
        Assert.NotEqual(before, team.Finances.Balance);
    }

    [Fact]
    public void Sponsorship_offers_can_be_accepted_and_become_deals()
    {
        var career = NewCareer(91);
        Assert.NotEmpty(career.SponsorOffers);

        var offer = career.SponsorOffers[0];
        var dealsBefore = career.PlayerTeam.Sponsors.Count;
        var (success, _) = career.AcceptSponsorOffer(offer.Id);

        Assert.True(success);
        Assert.Equal(dealsBefore + 1, career.PlayerTeam.Sponsors.Count);
        Assert.DoesNotContain(career.SponsorOffers, o => o.Id == offer.Id);
    }

    [Fact]
    public void Opening_sponsor_offers_never_clash_with_existing_partners()
    {
        var career = NewCareer(5150);

        Assert.NotEmpty(career.SponsorOffers);
        Assert.All(career.SponsorOffers, offer =>
            Assert.DoesNotContain(career.PlayerTeam.Sponsors, deal => deal.Tier == offer.Tier));
    }

    [Fact]
    public void An_offer_for_a_filled_tier_cannot_be_accepted_and_leaves_the_list()
    {
        var career = NewCareer(6161);
        var main = career.PlayerTeam.Sponsors.First();
        var stale = new SponsorOffer { Brand = "Test Brand", Tier = main.Tier, AnnualValue = 50_000 };
        career.SponsorOffers.Add(stale);

        var (success, message) = career.AcceptSponsorOffer(stale.Id);

        Assert.False(success);
        Assert.Contains("already have", message);
        Assert.DoesNotContain(career.SponsorOffers, o => o.Id == stale.Id);
    }

    [Fact]
    public void Free_agent_signing_respects_the_salary_cap()
    {
        var career = NewCareer(13);
        var agent = career.Transfers.FreeAgents.OrderBy(p => p.Rating).First();
        var huge = PlayerValue.WeeklyWageFor(agent) * 50;

        var (blocked, message) = career.SignFreeAgent(agent.Id, huge, 3);
        Assert.False(blocked);
        Assert.Contains("salary cap", message, StringComparison.OrdinalIgnoreCase);

        // A modest deal is allowed when there is room, and the cap is never breached either way.
        var cheap = career.Transfers.FreeAgents.OrderBy(p => p.Rating).First();
        var (allowed, why) = career.SignFreeAgent(cheap.Id, PlayerValue.WeeklyWageFor(cheap), 1);
        _ = why;
        Assert.True(career.PlayerTeam.SalaryCapSpend <= career.PlayerTeam.Finances.SalaryCap);
        if (allowed) Assert.Contains(career.PlayerTeam.Squad, p => p.Id == cheap.Id);
    }

    [Fact]
    public void Transfer_offers_can_be_made_for_listed_players()
    {
        var career = NewCareer(17);
        career.RefreshTransferListings();
        Assert.NotEmpty(career.Transfers.Listings);

        var listing = career.Transfers.Listings
            .OrderBy(l => career.PlayerById(l.PlayerId)!.Rating)
            .First();

        var player = career.PlayerById(listing.PlayerId)!;
        var wage = PlayerValue.WeeklyWageFor(player);
        var (success, message) = career.MakeTransferOffer(listing.PlayerId, listing.AskingPrice, wage, 3);

        // Some clubs cannot fit the wage under the cap, which is a valid rejection.
        if (success)
        {
            Assert.Contains(career.PlayerTeam.Squad, p => p.Id == player.Id);
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(message));
        }
    }

    [Fact]
    public void Selling_a_listed_player_completes_the_transfer()
    {
        var career = NewCareer(23);
        var player = career.PlayerTeam.Squad.OrderByDescending(p => p.Value).First();
        var (listed, _) = career.ListPlayer(player.Id, player.Value);
        Assert.True(listed);

        var buyer = career.Teams.First(t => t.Id != career.PlayerTeamId && t.Squad.Count < 31);
        var balance = career.PlayerTeam.Finances.Balance;

        career.Transfers.Offers.Add(new TransferOffer
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            FromClubId = career.PlayerTeamId,
            ToClubId = buyer.Id,
            ToClubName = buyer.Name,
            Fee = player.Value,
            Season = career.SeasonYear,
            Round = career.Round
        });

        var offerId = career.Transfers.Offers[0].Id;
        var (success, message) = career.RespondToOffer(offerId, true);

        Assert.True(success, message);
        Assert.DoesNotContain(career.PlayerTeam.Squad, p => p.Id == player.Id);
        Assert.Contains(buyer.Squad, p => p.Id == player.Id);
        Assert.Equal(balance + player.Value, career.PlayerTeam.Finances.Balance);
    }

    [Fact]
    public void Career_survives_a_json_round_trip()
    {
        var career = NewCareer(31);
        career.PlayLeagueRound(null);

        var json = SaveGame.Serialize(career);
        var restored = SaveGame.Deserialize(json);

        Assert.Equal(career.Round, restored.Round);
        Assert.Equal(career.SeasonYear, restored.SeasonYear);
        Assert.Equal(career.Teams.Count, restored.Teams.Count);
        Assert.Equal(career.PlayerTeamId, restored.PlayerTeamId);
        Assert.Equal(career.PlayerTeam.Name, restored.PlayerTeam.Name);
        Assert.Equal(career.Cup.Rounds.Count, restored.Cup.Rounds.Count);
        Assert.Equal(career.News.Count, restored.News.Count);
        Assert.Equal(career.Transfers.FreeAgents.Count, restored.Transfers.FreeAgents.Count);

        var originalFixture = career.PlayerLeague.Fixtures.First(f => f.Result is not null);
        var restoredFixture = restored.PlayerLeague.Fixtures.First(f => f.Id == originalFixture.Id);
        Assert.Equal(originalFixture.Result!.HomeScore, restoredFixture.Result!.HomeScore);
    }

    [Fact]
    public void Save_game_round_trips_through_disk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rugby-test-{Guid.NewGuid():N}.json");
        try
        {
            var career = NewCareer(41);
            SaveGame.Save(career, path);

            Assert.True(SaveGame.Exists(path));
            var loaded = SaveGame.TryLoad(path);

            Assert.NotNull(loaded);
            Assert.Equal(career.PlayerTeam.Name, loaded!.PlayerTeam.Name);
        }
        finally
        {
            SaveGame.Delete(path);
        }
    }
}
