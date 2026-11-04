namespace RugbyLeague.Core.Model;

/// <summary>
/// The ten rated skills of a player. Every value is 1..99. Team strength in the match engine
/// is derived from these numbers, weighted by the role the player is picked in.
/// </summary>
public sealed class PlayerAttributes
{
    public int Pace { get; set; } = 55;
    public int Strength { get; set; } = 55;
    public int Passing { get; set; } = 55;
    public int Handling { get; set; } = 55;
    public int Kicking { get; set; } = 40;
    public int Tackling { get; set; } = 55;
    public int Vision { get; set; } = 55;
    public int Stamina { get; set; } = 55;
    public int Discipline { get; set; } = 55;
    public int GoalKicking { get; set; } = 35;

    private static readonly Dictionary<PlayerPosition, (double pace, double strength, double passing, double handling, double kicking, double tackling, double vision, double stamina, double discipline)> Weights = new()
    {
        [PlayerPosition.Fullback] = (1.3, 0.5, 1.0, 1.0, 0.9, 1.0, 1.2, 1.0, 0.8),
        [PlayerPosition.Wing] = (1.8, 0.7, 0.6, 1.0, 0.3, 0.7, 1.0, 1.0, 0.8),
        [PlayerPosition.Centre] = (1.2, 1.0, 1.1, 1.0, 0.4, 0.9, 1.0, 1.0, 0.8),
        [PlayerPosition.FiveEighth] = (0.9, 0.6, 1.4, 1.1, 1.2, 0.8, 1.4, 1.0, 0.9),
        [PlayerPosition.Halfback] = (0.9, 0.5, 1.5, 1.1, 1.3, 0.7, 1.5, 1.0, 0.9),
        [PlayerPosition.Prop] = (0.5, 1.8, 0.7, 1.0, 0.2, 1.4, 0.6, 1.1, 0.8),
        [PlayerPosition.Hooker] = (0.9, 1.2, 1.3, 1.0, 0.7, 1.2, 1.0, 1.1, 0.8),
        [PlayerPosition.SecondRow] = (0.8, 1.6, 0.8, 1.0, 0.2, 1.4, 0.7, 1.2, 0.8),
        [PlayerPosition.Lock] = (0.8, 1.5, 1.0, 1.0, 0.3, 1.4, 0.9, 1.3, 0.8)
    };

    public int OverallFor(PlayerPosition position)
    {
        var w = Weights[position];
        double total = w.pace + w.strength + w.passing + w.handling + w.kicking + w.tackling + w.vision + w.stamina + w.discipline;
        double score =
            Pace * w.pace + Strength * w.strength + Passing * w.passing + Handling * w.handling +
            Kicking * w.kicking + Tackling * w.tackling + Vision * w.vision + Stamina * w.stamina +
            Discipline * w.discipline;
        return (int)Math.Round(score / total);
    }

    public PlayerAttributes Clone() => (PlayerAttributes)MemberwiseClone();

    /// <summary>All skills as (name, value) pairs, used by the squad screen and training.</summary>
    public IReadOnlyList<(string Name, int Value)> Snapshot() => new[]
    {
        ("Pace", Pace), ("Strength", Strength), ("Passing", Passing), ("Handling", Handling),
        ("Kicking", Kicking), ("Tackling", Tackling), ("Vision", Vision), ("Stamina", Stamina),
        ("Discipline", Discipline), ("Goal Kicking", GoalKicking)
    };

    public int Get(string name) => name switch
    {
        nameof(Pace) => Pace,
        nameof(Strength) => Strength,
        nameof(Passing) => Passing,
        nameof(Handling) => Handling,
        nameof(Kicking) => Kicking,
        nameof(Tackling) => Tackling,
        nameof(Vision) => Vision,
        nameof(Stamina) => Stamina,
        nameof(Discipline) => Discipline,
        nameof(GoalKicking) => GoalKicking,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown attribute")
    };

    public void Add(string name, int delta, int cap = 99)
    {
        switch (name)
        {
            case nameof(Pace): Pace = Clamp(Pace + delta, cap); break;
            case nameof(Strength): Strength = Clamp(Strength + delta, cap); break;
            case nameof(Passing): Passing = Clamp(Passing + delta, cap); break;
            case nameof(Handling): Handling = Clamp(Handling + delta, cap); break;
            case nameof(Kicking): Kicking = Clamp(Kicking + delta, cap); break;
            case nameof(Tackling): Tackling = Clamp(Tackling + delta, cap); break;
            case nameof(Vision): Vision = Clamp(Vision + delta, cap); break;
            case nameof(Stamina): Stamina = Clamp(Stamina + delta, cap); break;
            case nameof(Discipline): Discipline = Clamp(Discipline + delta, cap); break;
            case nameof(GoalKicking): GoalKicking = Clamp(GoalKicking + delta, cap); break;
            default: throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown attribute");
        }
    }

    private static int Clamp(int value, int cap) => Math.Clamp(value, 1, Math.Clamp(cap, 1, 99));

    public static string[] AllNames { get; } =
    {
        nameof(Pace), nameof(Strength), nameof(Passing), nameof(Handling), nameof(Kicking),
        nameof(Tackling), nameof(Vision), nameof(Stamina), nameof(Discipline), nameof(GoalKicking)
    };
}
