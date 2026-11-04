using RugbyLeague.Core.Model;

namespace RugbyLeague.Core.Careers;

/// <summary>
/// Weekly training. Focus determines which attributes improve; a better training facility and a
/// younger squad mean faster development. Overtraining a tired squad risks knocks.
/// </summary>
public static class TrainingEngine
{
    public static List<TrainingInjury> RunWeekly(Team team, Random rng, int round)
    {
        var injuries = new List<TrainingInjury>();
        var facility = Math.Clamp(team.TrainingFacility, 1, 10);

        foreach (var player in team.Squad)
        {
            if (player.IsInjured) continue;

            // Condition recovers between matches, fastest for younger players.
            var recovery = 8 + facility + (player.Age < 24 ? 3 : 0);
            player.Fitness = Math.Clamp(player.Fitness + recovery, 15, 100);
            player.Sharpness = Math.Clamp(player.Sharpness - 2, 20, 100);

            // Form drifts toward what the player's ability deserves.
            var targetForm = 45 + player.Rating / 2;
            player.Form = Math.Clamp(player.Form + (targetForm - player.Form) / 6 + rng.Next(-3, 4), 20, 100);
            player.Morale = Math.Clamp(player.Morale + (player.Contract.YearsRemaining > 0 ? 1 : -2) + rng.Next(-2, 3), 20, 100);

            Develop(player, team.TrainingFocus, facility, rng, injuries, round);
            AgeDecline(player, rng);

            if (player.Fitness < 55 && rng.NextDouble() < 0.025)
            {
                var (type, _, weeks) = InjuryFactory.Create(rng);
                player.Injury = new Injury { Type = type, Severity = InjurySeverity.Knock, WeeksRemaining = Math.Max(1, weeks - 1), OccurredRound = round };
                injuries.Add(new TrainingInjury(player.Id, player.Name, type, player.Injury.WeeksRemaining));
            }
        }

        return injuries;
    }

    private static void Develop(Player player, TrainingFocus focus, int facility, Random rng,
        List<TrainingInjury> injuries, int round)
    {
        var ageFactor = player.Age switch
        {
            <= 20 => 1.5,
            <= 23 => 1.25,
            <= 26 => 1.0,
            <= 29 => 0.6,
            <= 32 => 0.3,
            _ => 0.1
        };

        var room = player.Potential - player.Rating;
        if (room > 0 && rng.NextDouble() < 0.45 * ageFactor * (0.6 + facility * 0.08))
        {
            var attribute = focus switch
            {
                TrainingFocus.Attack => Pick(rng, "Pace", "Passing", "Vision"),
                TrainingFocus.Defence => Pick(rng, "Tackling", "Strength", "Discipline"),
                TrainingFocus.Handling => Pick(rng, "Handling", "Passing", "Vision"),
                TrainingFocus.Kicking => Pick(rng, "Kicking", "GoalKicking"),
                TrainingFocus.Fitness => Pick(rng, "Stamina", "Strength", "Pace"),
                TrainingFocus.Recovery => Pick(rng, "Stamina", "Discipline"),
                _ => Pick(rng, PlayerAttributes.AllNames)
            };

            player.Attributes.Add(attribute, 1, Math.Min(99, player.Potential + 6));
        }

        // Heavy conditioning carries a small injury risk.
        if (focus == TrainingFocus.Fitness && rng.NextDouble() < 0.012)
        {
            player.Fitness = Math.Clamp(player.Fitness - 8, 15, 100);
            if (rng.NextDouble() < 0.4)
            {
                var (type, _, weeks) = InjuryFactory.Create(rng);
                player.Injury = new Injury { Type = type, Severity = InjurySeverity.Knock, WeeksRemaining = Math.Max(1, weeks - 1), OccurredRound = round };
                injuries.Add(new TrainingInjury(player.Id, player.Name, type, player.Injury.WeeksRemaining));
            }
        }
    }

    private static void AgeDecline(Player player, Random rng)
    {
        if (player.Age < 31) return;
        if (rng.NextDouble() > 0.18) return;

        var attribute = Pick(rng, "Pace", "Stamina", "Strength");
        player.Attributes.Add(attribute, -1);
    }

    private static string Pick(Random rng, params string[] options) => options[rng.Next(options.Length)];
}
