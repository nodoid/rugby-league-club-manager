using RugbyLeague.Core.Model;

namespace RugbyLeague.Core.Careers;

public enum NewsKind
{
    General,
    Match,
    Transfer,
    Injury,
    Finance,
    Sponsorship,
    Trophy,
    Promotion,
    Training
}

public sealed class NewsItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Season { get; set; }
    public int Round { get; set; }
    public NewsKind Kind { get; set; } = NewsKind.General;
    public string Headline { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public sealed class SeasonSummary
{
    public int Season { get; set; }
    public List<LeagueOutcome> Leagues { get; set; } = new();
    public string CupWinner { get; set; } = "";
    public string PlayerClub { get; set; } = "";
    public int PlayerClubPosition { get; set; }
    public List<string> Headlines { get; set; } = new();
    public bool PlayerClubPromoted { get; set; }
    public bool PlayerClubRelegated { get; set; }
    public List<string> Retirements { get; set; } = new();
}

public sealed class LeagueOutcome
{
    public string LeagueName { get; set; } = "";
    public string Champion { get; set; } = "";
    public string RunnerUp { get; set; } = "";
    public string MinorPremiers { get; set; } = "";
    public string WoodenSpoon { get; set; } = "";
    public string Promoted { get; set; } = "";
    public string Relegated { get; set; } = "";
}
