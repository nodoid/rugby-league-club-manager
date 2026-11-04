namespace RugbyLeague.Core.Model;

public sealed class CupRound
{
    public string Name { get; set; } = "Round 1";
    public int Index { get; set; }
    public List<Fixture> Fixtures { get; set; } = new();

    /// <summary>Clubs that received a bye and go straight into the next round.</summary>
    public List<Guid> Byes { get; set; } = new();

    public bool IsComplete => Fixtures.Count > 0 && Fixtures.All(f => f.IsPlayed);
    public bool IsEmpty => Fixtures.Count == 0 && Byes.Count == 0;
}

/// <summary>
/// A single-leg knockout competition. The Challenge Shield is open to every club in the world, and
/// the same class is reused for each league's end-of-season finals series.
/// </summary>
public sealed class Cup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Challenge Shield";
    public string ShortName { get; set; } = "Shield";
    public long PrizeMoney { get; set; } = 500_000;
    public List<CupRound> Rounds { get; set; } = new();
    public int CurrentRoundIndex { get; set; }
    public Guid? WinnerId { get; set; }
    public Guid LeagueId { get; set; }
    public bool IsComplete => WinnerId is not null;
    public bool IsFinalsSeries { get; set; }

    public CupRound? CurrentRound =>
        CurrentRoundIndex >= 0 && CurrentRoundIndex < Rounds.Count ? Rounds[CurrentRoundIndex] : null;

    public List<Guid> Entrants { get; set; } = new();

    /// <summary>The round a club is involved in, or null when eliminated.</summary>
    public CupRound? RoundInvolving(Guid teamId) =>
        Rounds.FirstOrDefault(r => r.Fixtures.Any(f => f.HomeTeamId == teamId || f.AwayTeamId == teamId));
}
