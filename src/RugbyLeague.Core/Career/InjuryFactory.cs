using RugbyLeague.Core.Model;

namespace RugbyLeague.Core.Careers;

public sealed record TrainingInjury(Guid PlayerId, string PlayerName, string Type, int Weeks);

public static class InjuryFactory
{
    private static readonly (string Type, InjurySeverity Severity, int MinWeeks, int MaxWeeks)[] Table =
    {
        ("Dead leg", InjurySeverity.Knock, 1, 1),
        ("Corked thigh", InjurySeverity.Knock, 1, 1),
        ("Stinger", InjurySeverity.Knock, 1, 1),
        ("Ankle sprain", InjurySeverity.Minor, 1, 2),
        ("Hamstring strain", InjurySeverity.Minor, 2, 3),
        ("Shoulder niggle", InjurySeverity.Minor, 1, 2),
        ("Calf tear", InjurySeverity.Moderate, 3, 5),
        ("Knee injury", InjurySeverity.Moderate, 3, 6),
        ("Fractured hand", InjurySeverity.Moderate, 4, 6),
        ("Broken arm", InjurySeverity.Severe, 6, 10),
        ("Fractured foot", InjurySeverity.Severe, 7, 11),
        ("ACL damage", InjurySeverity.SeasonEnding, 16, 24),
        ("Ruptured Achilles", InjurySeverity.SeasonEnding, 18, 26)
    };

    public static (string Type, InjurySeverity Severity, int Weeks) Create(Random rng)
    {
        // Weighted toward knocks and minor strains, with rare long-term injuries.
        var roll = rng.NextDouble();
        var index = roll switch
        {
            < 0.30 => rng.Next(0, 3),
            < 0.62 => rng.Next(3, 6),
            < 0.88 => rng.Next(6, 9),
            < 0.97 => rng.Next(9, 11),
            _ => rng.Next(11, Table.Length)
        };

        var entry = Table[index];
        var weeks = rng.Next(entry.MinWeeks, entry.MaxWeeks + 1);
        return (entry.Type, entry.Severity, weeks);
    }

    public static Injury ToInjury(Random rng, int round)
    {
        var (type, severity, weeks) = Create(rng);
        return new Injury { Type = type, Severity = severity, WeeksRemaining = weeks, OccurredRound = round };
    }
}
