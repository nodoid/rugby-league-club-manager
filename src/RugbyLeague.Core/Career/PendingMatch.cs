using RugbyLeague.Core.Model;

namespace RugbyLeague.Core.Careers;

/// <summary>The manager's next fixture and the competition it belongs to.</summary>
public sealed record PendingMatch(Fixture Fixture, CompetitionKind Kind, string CompetitionName, bool MustWin)
{
    public bool IsHome => Fixture.HomeTeamId != Guid.Empty;
}
