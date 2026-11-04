namespace RugbyLeague.Core.Model;

public sealed class MatchEvent
{
    public int Minute { get; set; }
    public MatchEventKind Kind { get; set; } = MatchEventKind.Commentary;
    public string Text { get; set; } = "";
    public Guid? TeamId { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
}

public sealed class FramePlayer
{
    public Guid TeamId { get; set; }
    public int Shirt { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
}

/// <summary>
/// One visual beat of a simulated match. Positions are absolute metres on a 100 x 68 pitch.
/// The top-down viewer interpolates between consecutive frames using <see cref="ReplaySeconds"/>.
/// </summary>
public sealed class MatchFrame
{
    public float Minute { get; set; }
    public float BallX { get; set; }
    public float BallY { get; set; }
    public Guid PossessionTeamId { get; set; }
    public int Tackle { get; set; }
    public string Action { get; set; } = "";
    public float ReplaySeconds { get; set; } = 2f;
    public List<FramePlayer> Players { get; set; } = new();
}

public sealed class MatchTeamStats
{
    public int Possession { get; set; }
    public int Tackles { get; set; }
    public int MissedTackles { get; set; }
    public int Metres { get; set; }
    public int LineBreaks { get; set; }
    public int Tries { get; set; }
    public int Errors { get; set; }
    public int Penalties { get; set; }
    public int Kicks { get; set; }
    public int FortyTwenties { get; set; }
    public int Completions { get; set; }
    public int Sets { get; set; }
    public int PossessionTicks { get; set; }

    public int CompletionRate => Sets == 0 ? 0 : (int)Math.Round(Completions * 100.0 / Sets);
}

public sealed class PlayerMatchStats
{
    public Guid PlayerId { get; set; }
    public Guid TeamId { get; set; }
    public string Name { get; set; } = "";
    public int Tries { get; set; }
    public int Goals { get; set; }
    public int DropGoals { get; set; }
    public int Metres { get; set; }
    public int Tackles { get; set; }
    public int MissedTackles { get; set; }
    public bool ManOfTheMatch { get; set; }
}

public sealed class MatchInjury
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public Guid TeamId { get; set; }
    public string Type { get; set; } = "Knock";
    public InjurySeverity Severity { get; set; } = InjurySeverity.Knock;
    public int WeeksOut { get; set; }
}

public sealed class MatchSetup
{
    public required TeamSheet Home { get; init; }
    public required TeamSheet Away { get; init; }
    public Weather Weather { get; init; } = new();
    public int Seed { get; init; } = 1;
    public string CompetitionName { get; init; } = "League";
    public bool MustHaveWinner { get; init; }
    public TacticalSettings HomeTactics { get; init; } = new() { Style = PlayingStyle.Balanced };
    public TacticalSettings AwayTactics { get; init; } = new() { Style = PlayingStyle.Balanced };

    /// <summary>When false the engine skips the visual timeline, which makes AI-only rounds cheaper.</summary>
    public bool CaptureFrames { get; init; } = true;
}

public sealed class MatchResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Competition { get; set; } = "League";
    public string RoundName { get; set; } = "";
    public string Venue { get; set; } = "";
    public WeatherKind Weather { get; set; } = WeatherKind.Overcast;

    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public string HomeTeamName { get; set; } = "";
    public string AwayTeamName { get; set; } = "";

    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public int HomeTries { get; set; }
    public int AwayTries { get; set; }
    public bool GoldenPoint { get; set; }

    public MatchTeamStats HomeStats { get; set; } = new();
    public MatchTeamStats AwayStats { get; set; } = new();
    public List<MatchEvent> Events { get; set; } = new();
    public List<MatchFrame> Frames { get; set; } = new();
    public List<PlayerMatchStats> PlayerStats { get; set; } = new();
    public List<MatchInjury> Injuries { get; set; } = new();

    public bool IsDraw => HomeScore == AwayScore;
    public Guid WinnerId => HomeScore > AwayScore ? HomeTeamId : AwayScore > HomeScore ? AwayTeamId : Guid.Empty;
    public Guid LoserId => HomeScore > AwayScore ? AwayTeamId : AwayScore > HomeScore ? HomeTeamId : Guid.Empty;
    public string ScoreLine => $"{HomeScore}-{AwayScore}";

    public MatchTeamStats StatsFor(Guid teamId) => teamId == HomeTeamId ? HomeStats : AwayStats;
    public string NameFor(Guid teamId) => teamId == HomeTeamId ? HomeTeamName : AwayTeamName;

    /// <summary>Commentary for the ticker, newest last.</summary>
    public IEnumerable<MatchEvent> Commentary => Events.Where(e => e.Kind is not MatchEventKind.Interchange);
}
