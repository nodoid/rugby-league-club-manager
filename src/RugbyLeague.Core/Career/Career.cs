using System.Text.Json.Serialization;
using RugbyLeague.Core.Data;
using RugbyLeague.Core.Model;
using RugbyLeague.Core.Sim;

namespace RugbyLeague.Core.Careers;

/// <summary>
/// The whole save game: every league, cup, club, player and contract. All state is plain data so it
/// round-trips through JSON.
/// </summary>
public sealed class Career
{
    /// <summary>Every league plays the same number of rounds so the calendar stays in sync.</summary>
    public const int SeasonRounds = 22;

    public int Seed { get; set; }
    public int SeasonYear { get; set; } = 2026;
    public string ManagerName { get; set; } = "Manager";
    public Guid PlayerTeamId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<League> Leagues { get; set; } = new();
    public List<Team> Teams { get; set; } = new();
    public Cup Cup { get; set; } = new();
    public List<Cup> FinalsSeries { get; set; } = new();

    public int Round { get; set; }
    public CompetitionPhase Phase { get; set; } = CompetitionPhase.Regular;
    public List<NewsItem> News { get; set; } = new();
    public List<SponsorOffer> SponsorOffers { get; set; } = new();
    public TransferMarket Transfers { get; set; } = new();
    public SeasonSummary? LastSeason { get; set; }

    /// <summary>Player-picked starting thirteen (shirt order) and bench; null means auto-select.</summary>
    public List<Guid>? LineupStarterIds { get; set; }
    public List<Guid>? LineupBenchIds { get; set; }

    [JsonIgnore] public Team PlayerTeam => TeamById(PlayerTeamId);
    [JsonIgnore] public League PlayerLeague => LeagueOf(PlayerTeamId);

    /// <summary>Australian clubs trade in dollars, English clubs in pounds.</summary>
    [JsonIgnore] public string CurrencySymbol => PlayerLeague.Country == "England" ? "£" : "$";
    [JsonIgnore] public int SeasonWeeks => Math.Max(1, Leagues.Max(l => l.Rounds));
    [JsonIgnore] public int MaxRounds => Leagues.Count == 0 ? 0 : Leagues.Max(l => l.Rounds);
    [JsonIgnore] public long Balance => PlayerTeam.Finances.Balance;
    [JsonIgnore] public long CapSpace => PlayerTeam.Finances.SalaryCap - PlayerTeam.SalaryCapSpend;
    [JsonIgnore] public Cup? PlayerFinalSeries =>
        FinalsSeries.FirstOrDefault(c => !c.IsComplete && c.Entrants.Contains(PlayerTeamId));
    [JsonIgnore] public bool SeasonOver => Phase == CompetitionPhase.OffSeason;

    // ---------------------------------------------------------------- lookups

    public Team TeamById(Guid id) => Teams.First(t => t.Id == id);
    public Team? TryTeam(Guid id) => Teams.FirstOrDefault(t => t.Id == id);
    public League LeagueById(Guid id) => Leagues.First(l => l.Id == id);
    public League LeagueOf(Guid teamId) => LeagueById(TeamById(teamId).LeagueId);

    public Player? PlayerById(Guid id) =>
        Teams.SelectMany(t => t.Squad).FirstOrDefault(p => p.Id == id) ??
        Transfers.FreeAgents.FirstOrDefault(p => p.Id == id);

    public List<StandingsRow> TableFor(League league) =>
        league.BuildTable(Teams.ToDictionary(t => t.Id, t => t), Round);

    public int PlayerLeaguePosition()
    {
        var league = PlayerLeague;
        var table = TableFor(league);
        for (var i = 0; i < table.Count; i++)
            if (table[i].TeamId == PlayerTeamId) return i + 1;
        return table.Count;
    }

    // ---------------------------------------------------------------- schedule queries

    public Fixture? NextLeagueFixture
    {
        get
        {
            if (Phase != CompetitionPhase.Regular) return null;
            var league = PlayerLeague;
            if (Round >= league.Rounds) return null;
            return league.FixtureFor(PlayerTeamId, Round);
        }
    }

    public bool PlayerHasBye => Phase == CompetitionPhase.Regular && NextLeagueFixture is null && Round < PlayerLeague.Rounds;

    public Fixture? NextCupFixture
    {
        get
        {
            if (Cup.IsComplete || Cup.CurrentRound is null) return null;
            return Cup.CurrentRound.Fixtures.FirstOrDefault(f =>
                !f.IsPlayed && (f.HomeTeamId == PlayerTeamId || f.AwayTeamId == PlayerTeamId));
        }
    }

    public Fixture? NextFinalsFixture =>
        PlayerFinalSeries?.CurrentRound?.Fixtures.FirstOrDefault(f =>
            !f.IsPlayed && (f.HomeTeamId == PlayerTeamId || f.AwayTeamId == PlayerTeamId));

    /// <summary>True when the player's club is still alive in the Challenge Shield this round.</summary>
    public bool PlayerInCupThisRound => NextCupFixture is not null;

    /// <summary>The next fixture the manager must deal with, if any.</summary>
    [JsonIgnore]
    public PendingMatch? PendingMatch
    {
        get
        {
            if (Phase == CompetitionPhase.OffSeason) return null;

            if (Phase == CompetitionPhase.Regular && CupRoundDue && NextCupFixture is { } cupFixture)
                return new PendingMatch(cupFixture, CompetitionKind.Cup, Cup.Name, true);

            if (Phase == CompetitionPhase.Finals && NextFinalsFixture is { } finalsFixture)
                return new PendingMatch(finalsFixture, CompetitionKind.Cup, "Finals Series", true);

            if (Phase == CompetitionPhase.Regular && NextLeagueFixture is { } leagueFixture)
                return new PendingMatch(leagueFixture, CompetitionKind.League, PlayerLeague.Name, false);

            return null;
        }
    }

    /// <summary>Submits the manager's result for the pending fixture and advances the world.</summary>
    public void SubmitPendingMatch(MatchResult result)
    {
        var pending = PendingMatch;
        if (pending is null) return;

        switch (pending.Kind)
        {
            case CompetitionKind.Cup when pending.CompetitionName == "Finals Series":
                PlayFinalsRound(result);
                break;
            case CompetitionKind.Cup:
                PlayCupRound(result);
                break;
            default:
                PlayLeagueRound(result);
                break;
        }
    }

    /// <summary>Builds the setup for the pending fixture so the client can pre-simulate it.</summary>
    public MatchSetup? PendingSetup(bool captureFrames)
    {
        var pending = PendingMatch;
        return pending is null
            ? null
            : BuildSetup(pending.Fixture, pending.CompetitionName, pending.MustWin, captureFrames);
    }

    /// <summary>Cup rounds are spaced through the league calendar; one becomes due every third week.</summary>
    public bool CupRoundDue => !Cup.IsComplete && Cup.CurrentRound is not null && Round >= 2 + Cup.CurrentRoundIndex * 3;

    /// <summary>
    /// Resolves any cup round whose scheduled week has arrived. Call this after the manager has had
    /// the chance to play their own cup tie; anything still outstanding is simulated.
    /// </summary>
    public void EnsureCupProgress()
    {
        var guard = 0;
        while (CupRoundDue && guard++ < 8)
        {
            PlayCupRound(null);
        }
    }

    // ---------------------------------------------------------------- match preparation

    public Weather WeatherForRound(int round)
    {
        var rng = new Random(Seed * 131 + round * 17 + SeasonYear * 7);
        var kind = rng.NextDouble() switch
        {
            < 0.30 => WeatherKind.Clear,
            < 0.55 => WeatherKind.Overcast,
            < 0.70 => WeatherKind.Showers,
            < 0.80 => WeatherKind.HeavyRain,
            < 0.90 => WeatherKind.Windy,
            _ => WeatherKind.Cold
        };

        var baseWind = kind switch
        {
            WeatherKind.Windy => 16,
            WeatherKind.HeavyRain => 12,
            WeatherKind.Showers => 9,
            WeatherKind.Cold => 6,
            WeatherKind.Clear => 4,
            _ => 5
        };

        return new Weather
        {
            Kind = kind,
            Temperature = 6 + rng.Next(0, 18),
            WindSpeed = baseWind + rng.Next(0, 8),
            WindBearing = rng.Next(0, 360)
        };
    }

    public int DeterministicSeed(Fixture fixture) =>
        (fixture.Id.GetHashCode() * 397) ^ (SeasonYear * 31) ^ (Round * 7);

    public TeamSheet BuildSheet(Team team, bool usePlayerLineup)
    {
        if (!usePlayerLineup || team.Id != PlayerTeamId || LineupStarterIds is null)
            return team.SelectBestXV();

        var starters = LineupStarterIds
            .Select(id => team.Squad.FirstOrDefault(p => p.Id == id))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();

        if (starters.Count != 13) return team.SelectBestXV();

        var benchIds = LineupBenchIds ?? new List<Guid>();
        var bench = benchIds
            .Select(id => team.Squad.FirstOrDefault(p => p.Id == id))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();

        if (bench.Count < 4)
        {
            bench = team.AvailablePlayers
                .Where(p => starters.All(s => s.Id != p.Id))
                .OrderByDescending(p => p.Rating)
                .Take(4)
                .ToList();
        }

        return new TeamSheet
        {
            TeamId = team.Id,
            TeamName = team.Name,
            Nickname = team.Nickname,
            PrimaryColor = team.PrimaryColor,
            SecondaryColor = team.SecondaryColor,
            Starters = starters,
            Bench = bench,
            Style = team.Tactics.Style,
            Formation = team.Formation.Clone()
        };
    }

    public MatchSetup BuildSetup(Fixture fixture, string competitionName, bool mustHaveWinner, bool captureFrames = false)
    {
        var home = TeamById(fixture.HomeTeamId);
        var away = TeamById(fixture.AwayTeamId);

        return new MatchSetup
        {
            Home = BuildSheet(home, true),
            Away = BuildSheet(away, false),
            Weather = WeatherForRound(Round),
            Seed = DeterministicSeed(fixture),
            CompetitionName = competitionName,
            MustHaveWinner = mustHaveWinner,
            HomeTactics = home.Tactics.Clone(),
            AwayTactics = away.Tactics.Clone(),
            CaptureFrames = captureFrames
        };
    }

    public bool IsPlayerFixture(Fixture fixture) =>
        fixture.HomeTeamId == PlayerTeamId || fixture.AwayTeamId == PlayerTeamId;

    public string OpponentName(Fixture fixture) =>
        TeamById(fixture.HomeTeamId == PlayerTeamId ? fixture.AwayTeamId : fixture.HomeTeamId).Name;

    // ---------------------------------------------------------------- playing fixtures

    public void PlayLeagueRound(MatchResult? playerResult = null)
    {
        if (Phase != CompetitionPhase.Regular)
            throw new InvalidOperationException("The league season is not in progress.");

        foreach (var league in Leagues)
        {
            if (Round >= league.Rounds) continue;

            foreach (var fixture in league.FixturesForRound(Round).Where(f => !f.IsBye && !f.IsPlayed).ToList())
            {
                var result = IsPlayerFixture(fixture) && playerResult is not null
                    ? playerResult
                    : MatchEngine.Simulate(BuildSetup(fixture, league.Name, false));

                fixture.Result = result;
                result.RoundName = $"Round {Round + 1}";
                ApplyResult(fixture, league.Name, result);
            }
        }

        Round++;
        EnsureCupProgress();
        RunWeeklyBookkeeping();

        if (Round >= MaxRounds)
            StartFinals();
    }

    public void PlayCupRound(MatchResult? playerResult = null)
    {
        if (Cup.IsComplete || Cup.CurrentRound is null) return;
        PlayKnockoutRound(Cup, playerResult);
    }

    public void PlayFinalsRound(MatchResult? playerResult = null)
    {
        foreach (var finals in FinalsSeries.Where(f => !f.IsComplete).ToList())
            PlayKnockoutRound(finals, playerResult);

        if (FinalsSeries.Count > 0 && FinalsSeries.All(f => f.IsComplete))
            CompleteSeason();
    }

    private void PlayKnockoutRound(Cup cup, MatchResult? playerResult)
    {
        var round = cup.CurrentRound;
        if (round is null || cup.IsComplete) return;

        if (round.Fixtures.Count == 0)
        {
            CompetitionFactory.Advance(cup, Array.Empty<Guid>());
            return;
        }

        var winners = new List<Guid>();
        foreach (var fixture in round.Fixtures.Where(f => !f.IsPlayed).ToList())
        {
            var result = IsPlayerFixture(fixture) && playerResult is not null
                ? playerResult
                : MatchEngine.Simulate(BuildSetup(fixture, cup.Name, true));

            fixture.Result = result;
            result.RoundName = round.Name;
            ApplyResult(fixture, cup.Name, result);
            winners.Add(result.WinnerId);
        }

        CompetitionFactory.Advance(cup, winners);

        if (cup.IsComplete && cup.WinnerId is Guid championId)
        {
            var champion = TeamById(championId);
            champion.Finances.Balance += cup.PrizeMoney;
            champion.Finances.SeasonRevenue += cup.PrizeMoney;
            AddNews(NewsKind.Trophy, $"{champion.Name} win the {cup.Name}",
                $"{champion.Name} have won the {cup.Name}, banking {ThemeMoney(cup.PrizeMoney)} in prize money.",
                PlayerTeamId == championId);
        }
    }

    // ---------------------------------------------------------------- applying results

    private void ApplyResult(Fixture fixture, string competitionName, MatchResult result)
    {
        var home = TeamById(fixture.HomeTeamId);
        var away = TeamById(fixture.AwayTeamId);
        var winner = result.WinnerId;
        var isPlayerMatch = IsPlayerFixture(fixture);

        foreach (var stat in result.PlayerStats)
        {
            var player = home.Squad.Concat(away.Squad).FirstOrDefault(p => p.Id == stat.PlayerId);
            if (player is null) continue;

            player.Season.Appearances++;
            player.Season.Tries += stat.Tries;
            player.Season.Goals += stat.Goals;
            player.Season.DropGoals += stat.DropGoals;
            player.Season.Metres += stat.Metres;
            player.Season.Tackles += stat.Tackles;
            if (stat.ManOfTheMatch) player.Season.ManOfTheMatch++;

            player.Career.Appearances++;
            player.Career.Tries += stat.Tries;
            player.Career.Goals += stat.Goals;
            player.Career.DropGoals += stat.DropGoals;
            player.Career.Metres += stat.Metres;
            player.Career.Tackles += stat.Tackles;

            player.Fitness = Math.Clamp(player.Fitness - (14 + player.Attributes.Stamina / 12), 10, 100);
            player.Sharpness = Math.Clamp(player.Sharpness + 9, 20, 100);

            var performance = stat.Tries * 9 + stat.Metres / 18 + stat.Tackles / 6;
            player.Form = Math.Clamp(player.Form + performance - 3, 15, 100);

            if (stat.ManOfTheMatch) player.Morale = Math.Clamp(player.Morale + 6, 20, 100);
        }

        foreach (var injury in result.Injuries)
        {
            var player = home.Squad.Concat(away.Squad).FirstOrDefault(p => p.Id == injury.PlayerId);
            if (player is null) continue;

            player.Injury = new Injury
            {
                Type = injury.Type,
                Severity = injury.Severity,
                WeeksRemaining = injury.WeeksOut,
                OccurredRound = Round
            };

            if (isPlayerMatch)
                AddNews(NewsKind.Injury, $"{player.Name} ruled out for {injury.WeeksOut} week(s)",
                    $"{player.Name} picked up {injury.Type.ToLowerInvariant()} in the {result.HomeTeamName} v {result.AwayTeamName} match.",
                    true);
        }

        // Matchday revenue and sponsor bonuses.
        if (!fixture.IsBye)
        {
            var attendance = AttendanceFor(home, away, fixture);
            var gate = (long)attendance * home.Finances.TicketPrice;
            home.Finances.Balance += gate;
            home.Finances.SeasonRevenue += gate;

            if (winner != Guid.Empty)
            {
                var winningTeam = TeamById(winner);
                foreach (var sponsor in winningTeam.Sponsors)
                {
                    winningTeam.Finances.Balance += sponsor.BonusPerWin;
                    winningTeam.Finances.SeasonRevenue += sponsor.BonusPerWin;
                }
            }
        }

        if (isPlayerMatch)
        {
            var opponent = fixture.HomeTeamId == PlayerTeamId ? result.AwayTeamName : result.HomeTeamName;
            var playerIsHome = fixture.HomeTeamId == PlayerTeamId;
            var playerScore = playerIsHome ? result.HomeScore : result.AwayScore;
            var opponentScore = playerIsHome ? result.AwayScore : result.HomeScore;
            var verdict = playerScore > opponentScore ? "win" : playerScore < opponentScore ? "loss" : "draw";

            AddNews(NewsKind.Match, $"{PlayerTeam.Name} {playerScore}-{opponentScore} {opponent}",
                $"{competitionName} {result.RoundName}: {PlayerTeam.Name} recorded a {verdict} against {opponent} at {result.Venue}.",
                true);
        }
    }

    private int AttendanceFor(Team home, Team away, Fixture fixture)
    {
        var rng = new Random(DeterministicSeed(fixture));
        var pull = 0.42 + home.Reputation / 100.0 * 0.32 + away.Reputation / 100.0 * 0.12;
        pull = Math.Clamp(pull + rng.NextDouble() * 0.08 - 0.04, 0.22, 0.99);
        return (int)(home.Capacity * pull);
    }

    // ---------------------------------------------------------------- weekly cycle

    private void RunWeeklyBookkeeping()
    {
        foreach (var team in Teams)
        {
            var league = LeagueById(team.LeagueId);
            var weeks = Math.Max(1, league.Rounds);

            var wages = team.WeeklyWageBill;
            team.Finances.Balance -= wages;
            team.Finances.SeasonCosts += wages;

            var sponsorship = team.Sponsors.Sum(s => (long)s.AnnualValue) / weeks;
            var tv = league.TvMoney / weeks;
            team.Finances.Balance += sponsorship + tv;
            team.Finances.SeasonRevenue += sponsorship + tv;

            var stadium = team.Finances.WeeklyStadiumCost;
            team.Finances.Balance -= stadium;
            team.Finances.SeasonCosts += stadium;

            var rng = new Random(Seed ^ team.Id.GetHashCode() ^ (Round * 7919) ^ (SeasonYear * 13));
            var injuries = TrainingEngine.RunWeekly(team, rng, Round);

            foreach (var injury in injuries.Where(i => team.Id == PlayerTeamId))
            {
                AddNews(NewsKind.Injury, $"{injury.PlayerName} picks up a knock in training",
                    $"{injury.PlayerName} is expected to miss {injury.Weeks} week(s) with {injury.Type.ToLowerInvariant()}.",
                    true);
            }

            foreach (var player in team.Squad)
            {
                if (player.Injury is null || !player.Injury.IsActive) continue;
                player.Injury.WeeksRemaining--;
                if (player.Injury.WeeksRemaining <= 0)
                {
                    player.Injury = null;
                    if (team.Id == PlayerTeamId)
                        AddNews(NewsKind.Injury, $"{player.Name} back in full training", $"{player.Name} has recovered and is available for selection.", false);
                }
            }
        }

        RunTransferMarket();
        RefreshSponsorOffers();

        if (PlayerTeam.Finances.Balance < 0)
        {
            AddNews(NewsKind.Finance, "Board concern over club finances",
                $"The club is {ThemeMoney(PlayerTeam.Finances.Balance)} in the red. The board expects the situation to be addressed.",
                true);
        }

        TrimNews();
    }

    private void RefreshSponsorOffers()
    {
        // Expired offers go, and so does any offer for a tier or brand the club already has a partner in.
        SponsorOffers.RemoveAll(o => o.ExpiresRound < Round
                                     || PlayerTeam.Sponsors.Any(s => s.Tier == o.Tier || s.Brand == o.Brand));

        var league = PlayerLeague;
        var rng = new Random(Seed ^ 0x5EA50 ^ (Round * 131) ^ SeasonYear);

        if (SponsorOffers.Count >= 3) return;
        if (rng.NextDouble() > 0.45) return;

        var usedTiers = PlayerTeam.Sponsors.Select(s => s.Tier)
            .Concat(SponsorOffers.Select(o => o.Tier))
            .ToHashSet();

        var available = Enum.GetValues<SponsorTier>().Where(t => !usedTiers.Contains(t)).ToList();
        if (available.Count == 0) return;

        var tier = available[rng.Next(available.Count)];
        SponsorOffers.Add(SponsorshipEngine.CreateOffer(PlayerTeam, league, rng, Round, tier));
    }

    public (bool Success, string Message) AcceptSponsorOffer(Guid offerId)
    {
        var offer = SponsorOffers.FirstOrDefault(o => o.Id == offerId);
        if (offer is null) return (false, "That offer has expired.");

        if (PlayerTeam.Sponsors.Any(s => s.Tier == offer.Tier))
        {
            // The tier is already filled, so the offer can never be accepted: drop it for good.
            SponsorOffers.RemoveAll(o => o.Id == offer.Id || o.Tier == offer.Tier);
            return (false, $"You already have a {offer.TierLabel} partner, so that offer is off the table.");
        }

        PlayerTeam.Sponsors.Add(offer.ToDeal());

        // The accepted deal leaves the table, and so does any other offer for the same tier: the
        // club can only have one partner per tier.
        SponsorOffers.RemoveAll(o => o.Id == offer.Id || o.Tier == offer.Tier);
        AddNews(NewsKind.Sponsorship, $"{offer.Brand} join as {offer.TierLabel} partner",
            $"{offer.Brand} have agreed a {offer.Length}-season deal worth {ThemeMoney(offer.AnnualValue)} a year.", true);

        return (true, $"{offer.Brand} signed for {ThemeMoney(offer.AnnualValue)} a year.");
    }

    public void DeclineSponsorOffer(Guid offerId) => SponsorOffers.RemoveAll(o => o.Id == offerId);

    /// <summary>Cost of the next training-facility upgrade.</summary>
    public int TrainingUpgradeCost => 180_000 + PlayerTeam.TrainingFacility * 85_000;

    /// <summary>Cost of the next youth-academy upgrade.</summary>
    public int AcademyUpgradeCost => 150_000 + PlayerTeam.YouthAcademy * 70_000;

    public (bool Success, string Message) UpgradeTrainingFacility()
    {
        if (PlayerTeam.TrainingFacility >= 10) return (false, "The training facility is already world class.");
        if (PlayerTeam.Finances.Balance < TrainingUpgradeCost)
            return (false, $"The upgrade costs {ThemeMoney(TrainingUpgradeCost)}.");

        PlayerTeam.Finances.Balance -= TrainingUpgradeCost;
        PlayerTeam.Finances.SeasonCosts += TrainingUpgradeCost;
        PlayerTeam.TrainingFacility++;
        AddNews(NewsKind.Finance, "Training facility upgraded",
            $"{PlayerTeam.Name} have invested in their training base. Facility level is now {PlayerTeam.TrainingFacility}/10.",
            false);
        return (true, $"Training facility now level {PlayerTeam.TrainingFacility}.");
    }

    /// <summary>Cost of an extra goal-kicking session on the training paddock.</summary>
    public int KickingSessionCost => 6_000 + PlayerTeam.TrainingFacility * 500;

    /// <summary>
    /// A dedicated goal-kicking session. The two best kickers at the club work on their goal
    /// kicking; it is the quickest way to sharpen a kicker between rounds.
    /// </summary>
    public (bool Success, string Message) RunKickingSession()
    {
        var kickers = PlayerTeam.Squad
            .Where(p => !p.IsInjured)
            .OrderByDescending(p => p.Attributes.GoalKicking)
            .ThenByDescending(p => p.Attributes.Kicking)
            .Take(2)
            .ToList();

        if (kickers.Count == 0) return (false, "No fit players are available for a kicking session.");
        if (PlayerTeam.Finances.Balance < KickingSessionCost)
            return (false, $"A kicking session costs {ThemeMoney(KickingSessionCost)}.");

        PlayerTeam.Finances.Balance -= KickingSessionCost;
        PlayerTeam.Finances.SeasonCosts += KickingSessionCost;

        var improved = new List<string>();
        foreach (var player in kickers)
        {
            if (player.Attributes.GoalKicking >= 99) continue;
            player.Attributes.GoalKicking = Math.Min(99, player.Attributes.GoalKicking + 1);
            improved.Add(player.ShortName);
        }

        AddNews(NewsKind.Training, "Goal-kicking session",
            $"{PlayerTeam.Name} spent the week on goal kicking. {(improved.Count > 0 ? $"{string.Join(" and ", improved)} sharpened up." : "The kickers are already at their best.")}",
            false);

        return improved.Count == 0
            ? (true, "Session complete — your kickers are already at their best.")
            : (true, $"Kicking session complete: {string.Join(", ", improved)} improved.");
    }

    public (bool Success, string Message) UpgradeYouthAcademy()
    {
        if (PlayerTeam.YouthAcademy >= 10) return (false, "The youth academy is already world class.");
        if (PlayerTeam.Finances.Balance < AcademyUpgradeCost)
            return (false, $"The upgrade costs {ThemeMoney(AcademyUpgradeCost)}.");

        PlayerTeam.Finances.Balance -= AcademyUpgradeCost;
        PlayerTeam.Finances.SeasonCosts += AcademyUpgradeCost;
        PlayerTeam.YouthAcademy++;
        AddNews(NewsKind.Finance, "Youth academy upgraded",
            $"The academy is now level {PlayerTeam.YouthAcademy}/10, improving the quality of next season's graduates.",
            false);
        return (true, $"Youth academy now level {PlayerTeam.YouthAcademy}.");
    }

    private string ThemeMoney(long value) => $"{CurrencySymbol}{value:N0}";

    // ---------------------------------------------------------------- transfers

    public void RefreshTransferListings()
    {
        var rng = new Random(Seed ^ 0x7157 ^ (Round * 977) ^ SeasonYear);

        Transfers.Listings.RemoveAll(l =>
        {
            var club = TryTeam(l.ClubId);
            return club is null || club.Squad.All(p => p.Id != l.PlayerId);
        });

        foreach (var club in Teams.Where(t => t.Id != PlayerTeamId))
        {
            if (club.Squad.Count <= 22) continue;
            if (rng.NextDouble() > 0.12) continue;
            if (Transfers.Listings.Count >= 36) break;

            var candidate = club.Squad
                .Where(p => p.Morale < 60 || p.Rating < club.SquadRating - 4)
                .OrderBy(_ => rng.Next())
                .FirstOrDefault();

            if (candidate is null) continue;
            if (Transfers.Listings.Any(l => l.PlayerId == candidate.Id)) continue;

            Transfers.Listings.Add(new TransferListing
            {
                PlayerId = candidate.Id,
                ClubId = club.Id,
                AskingPrice = (int)(Math.Round(candidate.Value * (0.95 + rng.NextDouble() * 0.35) / 1000.0) * 1000),
                Season = SeasonYear
            });
        }
    }

    private void RunTransferMarket()
    {
        var rng = new Random(Seed ^ 0xA11CE ^ (Round * 3571) ^ SeasonYear);

        if (Round % 2 == 0) RefreshTransferListings();

        // Clubs bid for players the manager has listed.
        var myListings = Transfers.Listings.Where(l => l.ClubId == PlayerTeamId).ToList();
        foreach (var listing in myListings)
        {
            if (Transfers.PendingOffers.Any(o => o.PlayerId == listing.PlayerId)) continue;
            if (rng.NextDouble() > 0.4) continue;

            var player = PlayerTeam.Squad.FirstOrDefault(p => p.Id == listing.PlayerId);
            if (player is null) continue;

            var buyer = Teams.Where(t => t.Id != PlayerTeamId && t.Squad.Count < 30)
                .OrderBy(t => rng.Next())
                .FirstOrDefault();
            if (buyer is null) continue;

            var fee = (int)(Math.Round(player.Value * (0.85 + rng.NextDouble() * 0.4) / 1000.0) * 1000);

            Transfers.Offers.Add(new TransferOffer
            {
                PlayerId = player.Id,
                PlayerName = player.Name,
                FromClubId = PlayerTeamId,
                ToClubId = buyer.Id,
                ToClubName = buyer.Name,
                Fee = fee,
                Season = SeasonYear,
                Round = Round
            });

            AddNews(NewsKind.Transfer, $"{buyer.Name} bid ${fee:N0} for {player.Name}",
                $"{buyer.Name} have made an offer for {player.Name}. The bid is on your desk.", true);
        }

        // Offers older than two rounds lapse.
        foreach (var offer in Transfers.Offers.Where(o => o.Status == TransferStatus.Pending && o.Season == SeasonYear && Round - o.Round > 2))
        {
            offer.Status = TransferStatus.Expired;
        }

        // AI clubs replace departures from the free-agent pool.
        foreach (var club in Teams.Where(t => t.Id != PlayerTeamId && t.Squad.Count < 24))
        {
            var freeAgent = Transfers.FreeAgents.OrderByDescending(p => p.Rating).FirstOrDefault();
            if (freeAgent is null) break;
            Transfers.FreeAgents.Remove(freeAgent);
            freeAgent.Contract = new Contract
            {
                WeeklyWage = PlayerValue.WeeklyWageFor(freeAgent),
                YearsRemaining = 1 + rng.Next(0, 2),
                SignedSeason = SeasonYear
            };
            AssignShirt(club, freeAgent);
            club.Squad.Add(freeAgent);
        }
    }

    public (bool Success, string Message) ListPlayer(Guid playerId, int askingPrice)
    {
        var player = PlayerTeam.Squad.FirstOrDefault(p => p.Id == playerId);
        if (player is null) return (false, "That player is not in your squad.");
        if (PlayerTeam.Squad.Count <= 21) return (false, "The squad is too small to sell anyone else.");

        var existing = Transfers.ListingFor(playerId);
        if (existing is not null)
        {
            existing.AskingPrice = Math.Max(5_000, askingPrice);
            return (true, $"{player.Name} is listed at ${existing.AskingPrice:N0}.");
        }

        Transfers.Listings.Add(new TransferListing
        {
            PlayerId = playerId,
            ClubId = PlayerTeamId,
            AskingPrice = Math.Max(5_000, askingPrice),
            Season = SeasonYear
        });
        return (true, $"{player.Name} listed for transfer at ${askingPrice:N0}.");
    }

    public void UnlistPlayer(Guid playerId) => Transfers.Listings.RemoveAll(l => l.PlayerId == playerId && l.ClubId == PlayerTeamId);

    public (bool Success, string Message) RespondToOffer(Guid offerId, bool accept)
    {
        var offer = Transfers.Offers.FirstOrDefault(o => o.Id == offerId);
        if (offer is null || offer.Status != TransferStatus.Pending) return (false, "That offer is no longer available.");

        if (!accept)
        {
            offer.Status = TransferStatus.Rejected;
            return (true, "Offer rejected.");
        }

        var player = PlayerTeam.Squad.FirstOrDefault(p => p.Id == offer.PlayerId);
        if (player is null) return (false, "That player has already left the club.");
        if (PlayerTeam.Squad.Count <= 20) return (false, "You cannot sell any more players; the squad is at its minimum.");

        var buyer = TeamById(offer.ToClubId);
        PlayerTeam.Squad.Remove(player);
        Transfers.Listings.RemoveAll(l => l.PlayerId == player.Id);
        PlayerTeam.Finances.Balance += offer.Fee;
        PlayerTeam.Finances.SeasonRevenue += offer.Fee;

        AssignShirt(buyer, player);
        buyer.Squad.Add(player);
        offer.Status = TransferStatus.Accepted;

        AddNews(NewsKind.Transfer, $"{player.Name} joins {buyer.Name}",
            $"{player.Name} has completed a ${offer.Fee:N0} move to {buyer.Name}.", true);

        return (true, $"{player.Name} sold to {buyer.Name} for ${offer.Fee:N0}.");
    }

    public (bool Success, string Message) SignFreeAgent(Guid playerId, int weeklyWage, int years)
    {
        var player = Transfers.FreeAgents.FirstOrDefault(p => p.Id == playerId);
        if (player is null) return (false, "That player is no longer a free agent.");
        if (PlayerTeam.Squad.Count >= 34) return (false, "Your squad is full.");

        var demand = PlayerValue.WeeklyWageFor(player);
        if (weeklyWage < demand * 0.92) return (false, $"{player.Name} wants at least ${(int)(demand * 0.92):N0} per week.");

        var capImpact = weeklyWage * 52L;
        if (PlayerTeam.SalaryCapSpend + capImpact > PlayerTeam.Finances.SalaryCap)
            return (false, "That contract would take the club over the salary cap.");

        Transfers.FreeAgents.Remove(player);
        player.Contract = new Contract
        {
            WeeklyWage = weeklyWage,
            YearsRemaining = Math.Clamp(years, 1, 4),
            SignedSeason = SeasonYear
        };
        AssignShirt(PlayerTeam, player);
        PlayerTeam.Squad.Add(player);

        AddNews(NewsKind.Transfer, $"{player.Name} signs for {PlayerTeam.Name}",
            $"{player.Name} joins as a free agent on ${weeklyWage:N0} per week for {player.Contract.YearsRemaining} season(s).", true);

        return (true, $"{player.Name} has signed.");
    }

    public (bool Success, string Message) MakeTransferOffer(Guid playerId, int fee, int weeklyWage, int years)
    {
        var listing = Transfers.ListingFor(playerId);
        if (listing is null) return (false, "That player is not listed for transfer.");

        var seller = TeamById(listing.ClubId);
        var player = seller.Squad.FirstOrDefault(p => p.Id == playerId);
        if (player is null) return (false, "That player is no longer available.");
        if (PlayerTeam.Squad.Count >= 34) return (false, "Your squad is full.");
        if (fee > PlayerTeam.Finances.TransferBudget) return (false, "The club does not have the funds for that fee.");

        var demand = PlayerValue.WeeklyWageFor(player);
        if (weeklyWage < demand * 0.95)
            return (false, $"{player.Name}'s agent wants around ${demand:N0} per week.");

        if (PlayerTeam.SalaryCapSpend + weeklyWage * 52L > PlayerTeam.Finances.SalaryCap)
            return (false, "That contract would break the salary cap.");

        if (fee < listing.AskingPrice * 0.92)
            return (false, $"{seller.Name} reject the bid and are holding out for ${listing.AskingPrice:N0}.");

        seller.Squad.Remove(player);
        Transfers.Listings.Remove(listing);
        seller.Finances.Balance += fee;
        seller.Finances.SeasonRevenue += fee;
        PlayerTeam.Finances.Balance -= fee;
        PlayerTeam.Finances.SeasonCosts += fee;

        player.Contract = new Contract
        {
            WeeklyWage = weeklyWage,
            YearsRemaining = Math.Clamp(years, 1, 4),
            SignedSeason = SeasonYear
        };

        AssignShirt(PlayerTeam, player);
        PlayerTeam.Squad.Add(player);

        AddNews(NewsKind.Transfer, $"{player.Name} signs for {PlayerTeam.Name}",
            $"A ${fee:N0} fee has been agreed with {seller.Name} for {player.Name}.", true);

        return (true, $"{player.Name} has joined for ${fee:N0}.");
    }

    private void AssignShirt(Team team, Player player)
    {
        var used = team.Squad.Select(p => p.ShirtNumber).ToHashSet();
        var shirt = Enumerable.Range(1, 45).FirstOrDefault(n => !used.Contains(n), 45);
        player.ShirtNumber = shirt;
    }

    // ---------------------------------------------------------------- season end

    private void StartFinals()
    {
        Phase = CompetitionPhase.Finals;
        FinalsSeries.Clear();

        foreach (var league in Leagues.Where(l => l.FinalsSpots > 1))
        {
            var table = TableFor(league);
            var seeds = table.Take(league.FinalsSpots).Select(r => r.TeamId).ToList();
            var finals = CompetitionFactory.CreateKnockout(
                $"{league.ShortName} Finals", $"{league.ShortName} Finals", seeds, league.PrizeMoney, league.Id, isFinals: true);
            FinalsSeries.Add(finals);

            var playerIn = seeds.Contains(PlayerTeamId);
            AddNews(NewsKind.General, $"{league.Name} finals series confirmed",
                $"{TeamById(seeds[0]).Name} finished top and will lead the finals. " +
                (playerIn ? $"{PlayerTeam.Name} have qualified." : $"{PlayerTeam.Name} missed out."),
                playerIn);
        }

    }

    private void CompleteSeason()
    {
        var summary = new SeasonSummary { Season = SeasonYear, PlayerClub = PlayerTeam.Name };

        foreach (var league in Leagues)
        {
            var table = TableFor(league);
            var outcome = new LeagueOutcome
            {
                LeagueName = league.Name,
                MinorPremiers = table.Count > 0 ? table[0].TeamName : "",
                WoodenSpoon = table.Count > 0 ? table[^1].TeamName : ""
            };

            var finals = FinalsSeries.FirstOrDefault(f => f.LeagueId == league.Id);
            var championId = finals?.WinnerId ?? (table.Count > 0 ? table[0].TeamId : Guid.Empty);
            var runnerUpId = finals?.Rounds[^1].Fixtures.FirstOrDefault()?.Result?.LoserId ?? Guid.Empty;

            if (championId != Guid.Empty)
            {
                outcome.Champion = TeamById(championId).Name;
                var champion = TeamById(championId);
                champion.Finances.Balance += league.PrizeMoney;
                champion.Finances.SeasonRevenue += league.PrizeMoney;
                foreach (var sponsor in champion.Sponsors)
                    champion.Finances.Balance += sponsor.BonusPerTrophy;
            }

            if (runnerUpId != Guid.Empty) outcome.RunnerUp = TeamById(runnerUpId).Name;
            summary.Leagues.Add(outcome);

            if (table.Count > 0 && league.Tier <= 1)
                AddNews(NewsKind.Trophy, $"{outcome.Champion} crowned champions",
                    $"{league.Name}: {outcome.Champion} take the title. {outcome.MinorPremiers} finished top of the table, " +
                    $"{outcome.WoodenSpoon} take the wooden spoon.", true);
        }

        summary.CupWinner = Cup.WinnerId is Guid cupWinner ? TeamById(cupWinner).Name : "";
        summary.PlayerClubPosition = PlayerLeaguePosition();

        ApplyPromotionAndRelegation(summary);
        ProcessContractsAndRetirements(summary);

        LastSeason = summary;
        Phase = CompetitionPhase.OffSeason;
        SponsorOffers.Clear();
        RefreshTransferListings();

        AddNews(NewsKind.General, "Season complete - the off-season begins",
            "Contracts, retirements and the transfer market now take centre stage.", true);

        TrimNews();
    }

    private void ApplyPromotionAndRelegation(SeasonSummary summary)
    {
        // Each country runs its own two divisions, so Australia and England swap clubs separately.
        foreach (var country in Leagues.Where(l => l.HasPromotionRelegation)
                     .Select(l => l.Country).Distinct().ToList())
        {
            var top = Leagues.FirstOrDefault(l => l.Country == country && l.Tier == 1);
            var lower = Leagues.FirstOrDefault(l => l.Country == country && l.Tier == 2);
            if (top is null || lower is null) continue;
            PromoteAndRelegate(top, lower, summary);
        }
    }

    private void PromoteAndRelegate(League topLeague, League lowerLeague, SeasonSummary summary)
    {
        var topTable = TableFor(topLeague);
        var lowerTable = TableFor(lowerLeague);
        if (topTable.Count < 2 || lowerTable.Count < 2) return;

        var relegated = topTable[^1].TeamId;
        var relegatedName = TeamById(relegated).Name;
        var promoted = (FinalsSeries.FirstOrDefault(f => f.LeagueId == lowerLeague.Id)?.WinnerId) ?? lowerTable[0].TeamId;
        var promotedName = TeamById(promoted).Name;

        MoveClub(relegated, topLeague, lowerLeague);
        MoveClub(promoted, lowerLeague, topLeague);

        var outcomeTop = summary.Leagues.FirstOrDefault(l => l.LeagueName == topLeague.Name);
        if (outcomeTop is not null)
        {
            outcomeTop.Relegated = relegatedName;
            outcomeTop.Promoted = promotedName;
        }

        AddNews(NewsKind.Promotion, $"{promotedName} promoted, {relegatedName} relegated",
            $"{promotedName} go up as {lowerLeague.Name} champions, swapping places with {relegatedName}.", true);

        if (promoted == PlayerTeamId) summary.PlayerClubPromoted = true;
        if (relegated == PlayerTeamId) summary.PlayerClubRelegated = true;

        // Million-pound style relegation play-off: second-bottom of the top flight against the
        // beaten lower-league finalist.
        // The clubs already swapped divisions cannot appear again, so pick from the remaining
        // finishers (the promoted side may have finished second in the lower table).
        var playoffHost = topTable[^2].TeamId;
        var challenger = lowerTable
            .Select(r => r.TeamId)
            .Where(id => id != promoted && id != playoffHost)
            .ElementAtOrDefault(1);
        if (challenger == Guid.Empty) return;

        var playoff = new Fixture { Round = 0, HomeTeamId = playoffHost, AwayTeamId = challenger };

        var result = MatchEngine.Simulate(new MatchSetup
        {
            Home = BuildSheet(TeamById(playoffHost), TeamById(playoffHost).Id == PlayerTeamId),
            Away = BuildSheet(TeamById(challenger), TeamById(challenger).Id == PlayerTeamId),
            Weather = WeatherForRound(MaxRounds),
            Seed = DeterministicSeed(playoff),
            CompetitionName = "Relegation Play-off",
            MustHaveWinner = true,
            HomeTactics = TeamById(playoffHost).Tactics.Clone(),
            AwayTactics = TeamById(challenger).Tactics.Clone(),
            CaptureFrames = false
        });

        if (result.WinnerId == challenger)
        {
            var loserName = TeamById(playoffHost).Name;
            MoveClub(playoffHost, topLeague, lowerLeague);
            MoveClub(challenger, lowerLeague, topLeague);

            AddNews(NewsKind.Promotion, $"{TeamById(challenger).Name} win the relegation play-off",
                $"{TeamById(challenger).Name} beat {loserName} {result.ScoreLine} to swap divisions. {loserName} drop to {lowerLeague.Name}.",
                true);

            if (playoffHost == PlayerTeamId) summary.PlayerClubRelegated = true;
            if (challenger == PlayerTeamId) summary.PlayerClubPromoted = true;
        }
        else
        {
            AddNews(NewsKind.Promotion, $"{TeamById(playoffHost).Name} survive the play-off",
                $"{TeamById(playoffHost).Name} beat {TeamById(challenger).Name} {result.ScoreLine} to stay in {topLeague.Name}.",
                TeamById(playoffHost).Id == PlayerTeamId);
        }
    }

    private void MoveClub(Guid teamId, League from, League to)
    {
        from.TeamIds.Remove(teamId);
        if (!to.TeamIds.Contains(teamId)) to.TeamIds.Add(teamId);
        var team = TeamById(teamId);
        team.LeagueId = to.Id;
        team.Finances.SalaryCap = to.SalaryCap;
    }

    private void ProcessContractsAndRetirements(SeasonSummary summary)
    {
        var rng = new Random(Seed ^ 0xC0FFEE ^ SeasonYear);

        foreach (var team in Teams)
        {
            var departing = new List<Player>();

            foreach (var player in team.Squad.ToList())
            {
                player.Contract.YearsRemaining--;

                var retireChance = player.Age switch
                {
                    >= 36 => 0.9,
                    >= 34 => 0.5,
                    >= 32 => 0.18,
                    _ => 0.0
                };

                if (rng.NextDouble() < retireChance)
                {
                    departing.Add(player);
                    summary.Retirements.Add($"{player.Name} ({team.Name}, age {player.Age})");
                    if (team.Id == PlayerTeamId)
                        AddNews(NewsKind.General, $"{player.Name} announces retirement",
                            $"{player.Name} hangs up the boots after {player.Career.Appearances} career appearances.", true);
                    continue;
                }

                if (player.Contract.YearsRemaining <= 0)
                {
                    departing.Add(player);
                    player.Contract.YearsRemaining = 0;
                    Transfers.FreeAgents.Add(player);
                }
            }

            foreach (var player in departing) team.Squad.Remove(player);

            // Rebuild the squad with academy graduates.
            while (team.Squad.Count < 26)
            {
                var graduate = PlayerFactory.CreatePlayer(rng,
                    PlayerFactory.CreateSquadRole(rng),
                    Math.Clamp(team.YouthAcademy * 3 + team.Reputation - 35, 38, 70),
                    new HashSet<string>());

                graduate.Age = 18 + rng.Next(0, 3);
                graduate.Potential = Math.Clamp(graduate.Rating + rng.Next(8, 22), 55, 94);
                graduate.Contract = new Contract
                {
                    WeeklyWage = Math.Max(1_100, PlayerValue.WeeklyWageFor(graduate)),
                    YearsRemaining = 2,
                    SignedSeason = SeasonYear + 1
                };
                AssignShirt(team, graduate);
                team.Squad.Add(graduate);
            }

            var league = LeagueById(team.LeagueId);
            NormaliseWages(team, league.SalaryCap, rng);
            team.Finances.ResetSeasonTotals();
            foreach (var player in team.Squad)
            {
                player.Season = new PlayerSeasonStats();
                player.Fitness = Math.Clamp(player.Fitness + 25, 60, 100);
                player.Form = 55 + rng.Next(0, 20);
                player.Morale = Math.Clamp(player.Morale + 5, 30, 95);
            }

            foreach (var sponsor in team.Sponsors) sponsor.SeasonsRemaining--;
            team.Sponsors.RemoveAll(s => s.SeasonsRemaining <= 0);
        }
    }

    public void BeginNewSeason()
    {
        if (Phase != CompetitionPhase.OffSeason) return;

        SeasonYear++;
        Round = 0;
        Phase = CompetitionPhase.Regular;
        FinalsSeries.Clear();
        LineupStarterIds = null;
        LineupBenchIds = null;

        var rng = new Random(Seed ^ SeasonYear);

        foreach (var league in Leagues)
        {
            league.Fixtures = CompetitionFactory.GenerateDoubleRoundRobin(league.TeamIds, rng, SeasonRounds);
        }

        var seeds = Teams.OrderByDescending(t => t.Reputation).ThenBy(t => t.Id).Select(t => t.Id).ToList();
        Cup = CompetitionFactory.CreateKnockout("Challenge Shield", "Shield", seeds, 600_000);

        Transfers.Listings.Clear();
        Transfers.Offers.Clear();
        Transfers.FreeAgents = Enumerable.Range(0, 14)
            .Select(_ => PlayerFactory.CreateFreeAgent(rng, SeasonYear, new HashSet<string>()))
            .ToList();

        foreach (var team in Teams)
        {
            foreach (var sponsor in team.Sponsors.Where(s => s.SeasonsRemaining <= 0)) sponsor.SeasonsRemaining = 1;
            if (team.Sponsors.All(s => s.Tier != SponsorTier.Main))
                team.Sponsors.Add(SponsorshipEngine.CreateInitialDeal(team, LeagueById(team.LeagueId), rng, SponsorTier.Main));
        }

        SponsorOffers.Clear();
        for (var i = 0; i < 2; i++)
        {
            // Never offer a tier the club already has a partner in.
            var available = Enum.GetValues<SponsorTier>()
                .Where(t => PlayerTeam.Sponsors.All(s => s.Tier != t) && SponsorOffers.All(o => o.Tier != t))
                .ToList();

            if (available.Count == 0) break;
            SponsorOffers.Add(SponsorshipEngine.CreateOffer(PlayerTeam, PlayerLeague, rng, 0,
                available[rng.Next(available.Count)]));
        }

        AddNews(NewsKind.General, $"Season {SeasonYear} kicks off",
            $"{PlayerTeam.Name} begin the new campaign in the {PlayerLeague.Name}.", true);

        TrimNews();
    }

    // ---------------------------------------------------------------- creation helpers

    /// <summary>
    /// Builds the full world. When <paramref name="playerTeamId"/> is null or unknown the manager is
    /// placed at the first club in the NRL; call <see cref="TakeCharge"/> to move to another club.
    /// </summary>
    public static Career CreateNew(Guid? playerTeamId = null, string managerName = "Manager", int seed = 12345,
        int seasonYear = 2026)
    {
        var rng = new Random(seed);
        var career = new Career
        {
            Seed = seed,
            SeasonYear = seasonYear,
            ManagerName = string.IsNullOrWhiteSpace(managerName) ? "Manager" : managerName,
            PlayerTeamId = playerTeamId ?? Guid.Empty
        };

        career.Leagues = TeamDatabase.BuildLeagues().ToList();
        var nrl = career.LeagueById(TeamDatabase.NrlId);
        var superLeague = career.LeagueById(TeamDatabase.SuperLeagueId);
        var championship = career.LeagueById(TeamDatabase.ChampionshipId);

        var formationIndex = 0;
        void AddClubs(League league, TeamDatabase.ClubSeed[] seeds)
        {
            foreach (var seedClub in seeds)
            {
                var team = new Team
                {
                    Name = $"{seedClub.City} {seedClub.Nickname}",
                    City = seedClub.City,
                    Nickname = seedClub.Nickname,
                    ShortName = seedClub.ShortName,
                    PrimaryColor = seedClub.Primary,
                    SecondaryColor = seedClub.Secondary,
                    StadiumName = seedClub.Stadium,
                    Capacity = seedClub.Capacity,
                    Reputation = seedClub.Reputation,
                    LeagueId = league.Id,
                    Style = seedClub.Style,
                    TrainingFacility = Math.Clamp(4 + seedClub.Reputation / 20, 3, 9),
                    YouthAcademy = Math.Clamp(4 + seedClub.Reputation / 22, 3, 9)
                };

                var presets = Formation.Presets();
                team.Formation = presets[formationIndex++ % presets.Count].Clone();
                team.Tactics.Style = seedClub.Style;
                team.Finances.SalaryCap = league.SalaryCap;
                team.Finances.TicketPrice = league.Tier == 1 ? 34 : 22;

                team.Squad = PlayerFactory.CreateSquad(team, rng);
                NormaliseWages(team, league.SalaryCap, rng);

                team.Sponsors.Add(SponsorshipEngine.CreateInitialDeal(team, league, rng, SponsorTier.Main));
                team.Sponsors.Add(SponsorshipEngine.CreateInitialDeal(team, league, rng));

                career.Teams.Add(team);
                league.TeamIds.Add(team.Id);
            }
        }

        AddClubs(nrl, TeamDatabase.NrlClubs);
        AddClubs(career.LeagueById(TeamDatabase.NrlChampionshipId), TeamDatabase.NrlChampionshipClubs);
        AddClubs(superLeague, TeamDatabase.SuperLeagueClubs);
        AddClubs(championship, TeamDatabase.ChampionshipClubs);

        if (career.Teams.All(t => t.Id != career.PlayerTeamId))
            career.PlayerTeamId = nrl.TeamIds[0];

        foreach (var league in career.Leagues)
            league.Fixtures = CompetitionFactory.GenerateDoubleRoundRobin(league.TeamIds, rng, SeasonRounds);

        var cupSeeds = career.Teams
            .OrderByDescending(t => t.Reputation)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => t.Id)
            .ToList();

        career.Cup = CompetitionFactory.CreateKnockout("Challenge Shield", "Shield", cupSeeds, 600_000);

        career.Transfers.FreeAgents = Enumerable.Range(0, 16)
            .Select(_ => PlayerFactory.CreateFreeAgent(rng, seasonYear, new HashSet<string>()))
            .ToList();

        career.Transfers.WindowClosesRound = 8;
        career.RefreshTransferListings();

        var openingTier = Enum.GetValues<SponsorTier>()
            .FirstOrDefault(t => career.PlayerTeam.Sponsors.All(s => s.Tier != t));
        if (career.PlayerTeam.Sponsors.All(s => s.Tier != openingTier))
            career.SponsorOffers.Add(
                SponsorshipEngine.CreateOffer(career.PlayerTeam, career.PlayerLeague, rng, 0, openingTier));

        career.AddNews(NewsKind.General, $"Welcome to {career.PlayerTeam.Name}",
            $"{career.ManagerName} takes charge at {career.PlayerTeam.Name} ahead of the {seasonYear} season. " +
            $"The board expects a top-half finish in the {career.PlayerLeague.Name}.", true);

        return career;
    }

    /// <summary>Switches the manager to a different club; used by the team-select screen.</summary>
    public void TakeCharge(Guid teamId, string managerName)
    {
        if (Teams.All(t => t.Id != teamId)) return;

        PlayerTeamId = teamId;
        if (!string.IsNullOrWhiteSpace(managerName)) ManagerName = managerName;
        LineupStarterIds = null;
        LineupBenchIds = null;
        SponsorOffers.Clear();
        News.Clear();

        var rng = new Random(Seed ^ teamId.GetHashCode() ^ SeasonYear);
        // Only offer tiers the new club has no partner in, otherwise accepting could never work.
        var openTiers = Enum.GetValues<SponsorTier>()
            .Where(t => PlayerTeam.Sponsors.All(s => s.Tier != t))
            .OrderBy(_ => rng.Next())
            .Take(2);
        foreach (var tier in openTiers)
            SponsorOffers.Add(SponsorshipEngine.CreateOffer(PlayerTeam, PlayerLeague, rng, Round, tier));

        AddNews(NewsKind.General, $"Welcome to {PlayerTeam.Name}",
            $"{ManagerName} takes charge at {PlayerTeam.Name} in the {PlayerLeague.Name}. The board wants a competitive season.", true);
    }

    private static void NormaliseWages(Team team, int salaryCap, Random rng)
    {
        var topTwentyFive = team.Squad
            .OrderByDescending(p => p.Contract.WeeklyWage)
            .Take(25)
            .Sum(p => (long)p.Contract.WeeklyWage * 52);

        if (topTwentyFive <= salaryCap) return;

        var factor = salaryCap * 0.94 / topTwentyFive;
        foreach (var player in team.Squad)
        {
            player.Contract.WeeklyWage = Math.Max(1_100, (int)(Math.Round(player.Contract.WeeklyWage * factor / 10.0) * 10));
            _ = rng;
        }
    }

    // ---------------------------------------------------------------- news

    public void AddNews(NewsKind kind, string headline, string body, bool important)
    {
        News.Insert(0, new NewsItem
        {
            Season = SeasonYear,
            Round = Round,
            Kind = kind,
            Headline = headline,
            Body = body,
            IsRead = !important
        });
    }

    private void TrimNews()
    {
        const int max = 240;
        if (News.Count > max) News.RemoveRange(max, News.Count - max);
    }

    public int UnreadNewsCount => News.Count(n => !n.IsRead);

    public void MarkNewsRead()
    {
        foreach (var item in News) item.IsRead = true;
    }
}
