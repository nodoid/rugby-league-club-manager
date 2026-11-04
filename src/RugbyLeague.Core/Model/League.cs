namespace RugbyLeague.Core.Model;

public sealed class Fixture
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Round { get; set; }

    /// <summary>Home side. For a bye round there is no away side.</summary>
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }

    public MatchResult? Result { get; set; }

    public bool IsBye => AwayTeamId == Guid.Empty;
    public bool IsPlayed => Result is not null;
}

public sealed class StandingsRow
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = "";
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int Byes { get; set; }
    public int PointsFor { get; set; }
    public int PointsAgainst { get; set; }

    public int Points { get; set; }
    public int Difference => PointsFor - PointsAgainst;
    public List<string> Form { get; set; } = new();

    public string FormString => string.Join(" ", Form.TakeLast(5));
}

public sealed class League
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "League";
    public string ShortName { get; set; } = "LG";

    /// <summary>1 is the top division of a country. Higher numbers are lower divisions.</summary>
    public int Tier { get; set; } = 1;

    public string Country { get; set; } = "Australia";
    public bool HasPromotionRelegation { get; set; }
    public int PromotionSpots { get; set; }
    public int RelegationSpots { get; set; }

    /// <summary>Teams that enter the end-of-season finals series (0 means no finals).</summary>
    public int FinalsSpots { get; set; } = 8;

    public int PointsForWin { get; set; } = 2;
    public int PointsForDraw { get; set; } = 1;
    public int ByePoints { get; set; }

    public int SalaryCap { get; set; } = 4_400_000;
    public long TvMoney { get; set; } = 4_000_000;
    public long PrizeMoney { get; set; } = 400_000;

    public List<Guid> TeamIds { get; set; } = new();
    public List<Fixture> Fixtures { get; set; } = new();

    public int Rounds => Fixtures.Count == 0 ? 0 : Fixtures.Max(f => f.Round) + 1;

    public IEnumerable<Fixture> FixturesForRound(int round) => Fixtures.Where(f => f.Round == round);

    public Fixture? FixtureFor(Guid teamId, int round) =>
        Fixtures.FirstOrDefault(f => f.Round == round && !f.IsBye && (f.HomeTeamId == teamId || f.AwayTeamId == teamId));

    /// <summary>League table ordered by points, then for-and-against difference, then points scored.</summary>
    public List<StandingsRow> BuildTable(IReadOnlyDictionary<Guid, Team> teams, int throughRound = int.MaxValue)
    {
        var rows = TeamIds.ToDictionary(id => id, id => new StandingsRow
        {
            TeamId = id,
            TeamName = teams.TryGetValue(id, out var t) ? t.Name : "Unknown"
        });

        foreach (var fixture in Fixtures.Where(f => f.IsPlayed || f.IsBye).OrderBy(f => f.Round))
        {
            var result = fixture.Result;

            if (fixture.IsBye)
            {
                // A bye only counts once that round has actually been played.
                if (fixture.Round >= throughRound) continue;
                if (rows.TryGetValue(fixture.HomeTeamId, out var byeRow))
                {
                    byeRow.Byes++;
                    byeRow.Points += ByePoints;
                }
                continue;
            }

            if (result is null) continue;

            if (!rows.TryGetValue(fixture.HomeTeamId, out var home) || !rows.TryGetValue(fixture.AwayTeamId, out var away))
                continue;

            home.Played++;
            away.Played++;
            home.PointsFor += result.HomeScore;
            home.PointsAgainst += result.AwayScore;
            away.PointsFor += result.AwayScore;
            away.PointsAgainst += result.HomeScore;

            if (result.HomeScore > result.AwayScore)
            {
                home.Won++;
                away.Lost++;
                home.Points += PointsForWin;
                home.Form.Add("W");
                away.Form.Add("L");
            }
            else if (result.AwayScore > result.HomeScore)
            {
                away.Won++;
                home.Lost++;
                away.Points += PointsForWin;
                away.Form.Add("W");
                home.Form.Add("L");
            }
            else
            {
                home.Drawn++;
                away.Drawn++;
                home.Points += PointsForDraw;
                away.Points += PointsForDraw;
                home.Form.Add("D");
                away.Form.Add("D");
            }
        }

        return rows.Values
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.Difference)
            .ThenByDescending(r => r.PointsFor)
            .ThenBy(r => r.TeamName, StringComparer.Ordinal)
            .ToList();
    }

    public int PositionOf(Guid teamId, IReadOnlyDictionary<Guid, Team> teams)
    {
        var table = BuildTable(teams);
        for (var i = 0; i < table.Count; i++)
            if (table[i].TeamId == teamId)
                return i + 1;
        return table.Count;
    }
}
