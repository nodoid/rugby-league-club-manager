namespace RugbyLeague.Core.Model;

public sealed class PlayerSeasonStats
{
    public int Appearances { get; set; }
    public int Tries { get; set; }
    public int Goals { get; set; }
    public int DropGoals { get; set; }
    public int Metres { get; set; }
    public int Tackles { get; set; }
    public int ManOfTheMatch { get; set; }
    public int SinBins { get; set; }
    public int Points => Tries * 4 + Goals * 2 + DropGoals;

    public void Add(PlayerSeasonStats other)
    {
        Appearances += other.Appearances;
        Tries += other.Tries;
        Goals += other.Goals;
        DropGoals += other.DropGoals;
        Metres += other.Metres;
        Tackles += other.Tackles;
        ManOfTheMatch += other.ManOfTheMatch;
        SinBins += other.SinBins;
    }

    public PlayerSeasonStats Clone() => (PlayerSeasonStats)MemberwiseClone();
}

public sealed class Contract
{
    public int WeeklyWage { get; set; }
    public int YearsRemaining { get; set; } = 1;
    public int SignedSeason { get; set; }

    public int AnnualValue => WeeklyWage * 52;
}

public sealed class Injury
{
    public string Type { get; set; } = "Knock";
    public InjurySeverity Severity { get; set; } = InjurySeverity.Knock;
    public int WeeksRemaining { get; set; }
    public int OccurredRound { get; set; }

    public bool IsActive => WeeksRemaining > 0;
    public string Description => $"{Type} ({WeeksRemaining}w)";
}

/// <summary>A single player. All ratings are 1..99 unless stated.</summary>
public sealed class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = "New";
    public string LastName { get; set; } = "Signing";
    public int ShirtNumber { get; set; } = 30;
    public PlayerPosition Position { get; set; } = PlayerPosition.Lock;
    public int Age { get; set; } = 24;
    public PlayerAttributes Attributes { get; set; } = new();
    public int Potential { get; set; } = 80;

    /// <summary>Recent form, 0..100. Impacts match strength; drifts toward the player's rating each week.</summary>
    public int Form { get; set; } = 60;

    /// <summary>Condition, 0..100. Low condition means a tired, injury-prone and error-prone player.</summary>
    public int Fitness { get; set; } = 100;

    /// <summary>Match sharpness, 0..100. Improves by playing, decays when rested.</summary>
    public int Sharpness { get; set; } = 65;

    public int Morale { get; set; } = 65;
    public Injury? Injury { get; set; }
    public Contract Contract { get; set; } = new();
    public PlayerSeasonStats Season { get; set; } = new();
    public PlayerSeasonStats Career { get; set; } = new();

    public string Name => $"{FirstName} {LastName}";
    public string ShortName => $"{FirstName[0]}. {LastName}";
    public bool IsInjured => Injury is { IsActive: true };
    public int Rating => Attributes.OverallFor(Position);
    public bool IsAvailable => !IsInjured;
    public int Value => PlayerValue.Calculate(this);
    public int AgeBestYearsLeft => Math.Max(0, 30 - Age);

    public override string ToString() => $"{Name} ({Position}, {Rating})";
}

/// <summary>Dollar values are whole dollars; valuations scale with rating, age and potential.</summary>
public static class PlayerValue
{
    public static int Calculate(Player player)
    {
        var rating = player.Rating;
        var baseValue = (rating - 40) * (rating - 40) * 480.0;   // 40 => 0, 80 => 768k, 92 => 1.3M
        var ageFactor = player.Age switch
        {
            <= 20 => 1.35,
            <= 23 => 1.45,
            <= 27 => 1.2,
            <= 30 => 0.85,
            <= 33 => 0.45,
            _ => 0.2
        };
        var potentialFactor = 1 + Math.Max(0, player.Potential - rating) / 100.0;
        var value = baseValue * ageFactor * potentialFactor;
        return (int)Math.Max(15_000, Math.Round(value / 1000.0) * 1000);
    }

    /// <summary>Expected wage demand for a player of this quality.</summary>
    public static int WeeklyWageFor(Player player)
    {
        var rating = player.Rating;
        var wage = 320 + Math.Pow(Math.Max(0, rating - 40), 2.3) * 1.95;
        var ageFactor = player.Age <= 22 ? 0.8 : player.Age >= 32 ? 0.85 : 1.0;
        return (int)Math.Max(1_100, Math.Round(wage * ageFactor / 10.0) * 10);
    }
}
