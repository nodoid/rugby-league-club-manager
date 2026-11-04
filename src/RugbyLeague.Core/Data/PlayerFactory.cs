using RugbyLeague.Core.Model;

namespace RugbyLeague.Core.Data;

/// <summary>
/// Generates fictional players. First and last names are drawn from pools of common rugby-league
/// given names and surnames; no generated name is intended to refer to a real person.
/// </summary>
public static class PlayerFactory
{
    private static readonly string[] FirstNames =
    {
        "Jarrod", "Kieran", "Brodie", "Tevita", "Rhys", "Callum", "Lachlan", "Marley", "Owen", "Dylan",
        "Nate", "Cooper", "Ethan", "Bailey", "Hugo", "Isaac", "Jaxon", "Kyle", "Liam", "Mitchell",
        "Noah", "Riley", "Sean", "Toby", "Zac", "Aiden", "Blake", "Cameron", "Dane", "Elliot",
        "Finn", "Grady", "Harrison", "Iestyn", "Joel", "Kade", "Lewis", "Morgan", "Niall", "Oscar",
        "Pieter", "Reuben", "Sione", "Trent", "Vaughan", "Wesley", "Xavier", "Zane", "Alfie", "Ben"
    };

    private static readonly string[] LastNames =
    {
        "Kelleher", "Whitcombe", "Farrar", "Ngata", "Brogan", "Halstead", "Mackay", "Tuiloma", "Reddish",
        "Ashcroft", "Bellinger", "Cathcart", "Dunmore", "Eckersley", "Fenwick", "Gallimore", "Hargreaves",
        "Iremonger", "Jessop", "Kirkbride", "Latimer", "Merrick", "Norbury", "Oakhurst", "Pemberton",
        "Quillan", "Rathbone", "Sedgwick", "Thornbury", "Underhill", "Vellacott", "Waverley", "Yardley",
        "Zammit", "Brayshaw", "Cudmore", "Dallimore", "Eldridge", "Furlonger", "Grimshaw", "Hollingworth",
        "Ingleby", "Jarrett", "Knowles", "Lomax", "Molyneux", "Naylor", "Oldfield", "Prescott", "Quarrie",
        "Sturgess", "Tattersall", "Ulyett", "Viney", "Winstanley", "Yeoman", "Ashby", "Bickerstaff",
        "Charnley", "Delaney", "Entwistle", "Fairhurst", "Gledhill", "Hodgkinson", "Ibbotson", "Jubb",
        "Kenyon", "Lythgoe", "Marsden", "Ogden", "Pilling", "Quarmby", "Rushworth", "Sutcliffe",
        "Varley", "Wadsworth", "Yates", "Ainsworth", "Boldon", "Cresswell"
    };

    private static readonly PlayerPosition[] ShirtRoles =
    {
        PlayerPosition.Fullback, PlayerPosition.Wing, PlayerPosition.Centre, PlayerPosition.Centre,
        PlayerPosition.Wing, PlayerPosition.FiveEighth, PlayerPosition.Halfback, PlayerPosition.Prop,
        PlayerPosition.Hooker, PlayerPosition.Prop, PlayerPosition.SecondRow, PlayerPosition.SecondRow,
        PlayerPosition.Lock
    };

    /// <summary>Per-role attribute bias applied on top of the player's core rating.</summary>
    public static IReadOnlyDictionary<string, int> RoleBias(PlayerPosition position) => position switch
    {
        PlayerPosition.Fullback => new Dictionary<string, int> { ["Pace"] = 8, ["Vision"] = 8, ["Passing"] = 4, ["Tackling"] = 3, ["Strength"] = -5, ["Kicking"] = 4 },
        PlayerPosition.Wing => new Dictionary<string, int> { ["Pace"] = 16, ["Vision"] = 2, ["Handling"] = 4, ["Tackling"] = -4, ["Strength"] = -4, ["Passing"] = -3, ["Kicking"] = -8 },
        PlayerPosition.Centre => new Dictionary<string, int> { ["Pace"] = 6, ["Strength"] = 4, ["Tackling"] = 2, ["Kicking"] = -8 },
        PlayerPosition.FiveEighth => new Dictionary<string, int> { ["Kicking"] = 10, ["Passing"] = 10, ["Vision"] = 12, ["Strength"] = -6, ["Tackling"] = -3 },
        PlayerPosition.Halfback => new Dictionary<string, int> { ["Kicking"] = 14, ["Passing"] = 12, ["Vision"] = 14, ["Strength"] = -7, ["Tackling"] = -4 },
        PlayerPosition.Prop => new Dictionary<string, int> { ["Strength"] = 14, ["Tackling"] = 6, ["Pace"] = -10, ["Kicking"] = -12, ["Vision"] = -6 },
        PlayerPosition.Hooker => new Dictionary<string, int> { ["Passing"] = 10, ["Stamina"] = 8, ["Tackling"] = 6, ["Vision"] = 4, ["Pace"] = -2 },
        PlayerPosition.SecondRow => new Dictionary<string, int> { ["Strength"] = 12, ["Stamina"] = 8, ["Tackling"] = 6, ["Pace"] = -6, ["Kicking"] = -12 },
        PlayerPosition.Lock => new Dictionary<string, int> { ["Strength"] = 10, ["Stamina"] = 10, ["Tackling"] = 8, ["Passing"] = 4, ["Kicking"] = -10 },
        _ => new Dictionary<string, int>()
    };

    public static List<Player> CreateSquad(Team team, Random rng)
    {
        var squad = new List<Player>();
        var usedShirts = new HashSet<int>();
        var usedNames = new HashSet<string>();

        // One first-choice player for each of the thirteen shirts in the club's formation.
        foreach (var slot in team.Formation.Slots.OrderBy(s => s.Shirt))
        {
            var player = CreatePlayer(rng, slot.Role, team.Reputation, usedNames);
            player.ShirtNumber = slot.Shirt;
            usedShirts.Add(slot.Shirt);
            squad.Add(player);
        }

        // Four bench forwards/backs plus depth so injuries bite but every role is covered.
        var depth = new List<PlayerPosition>
        {
            PlayerPosition.Prop, PlayerPosition.Hooker, PlayerPosition.SecondRow, PlayerPosition.Lock,
            PlayerPosition.Halfback, PlayerPosition.FiveEighth, PlayerPosition.Centre, PlayerPosition.Wing,
            PlayerPosition.Fullback, PlayerPosition.Prop, PlayerPosition.SecondRow, PlayerPosition.Wing,
            PlayerPosition.Centre, PlayerPosition.Hooker, PlayerPosition.Lock
        };

        var nextShirt = 14;
        foreach (var role in depth)
        {
            var player = CreatePlayer(rng, role, team.Reputation - rng.Next(0, 5), usedNames);
            while (usedShirts.Contains(nextShirt)) nextShirt++;
            player.ShirtNumber = nextShirt;
            usedShirts.Add(nextShirt);
            squad.Add(player);
        }

        EnsureGoalKicker(squad, rng);
        return squad;
    }

    public static Player CreatePlayer(Random rng, PlayerPosition position, int clubReputation, HashSet<string> usedNames)
    {
        string first, last, full;
        do
        {
            first = FirstNames[rng.Next(FirstNames.Length)];
            last = LastNames[rng.Next(LastNames.Length)];
            full = first + " " + last;
        } while (!usedNames.Add(full));

        var age = WeightedAge(rng);
        var baseRating = Math.Clamp(
            (int)Math.Round(Gaussian(rng, clubReputation + (age < 22 ? -4 : 0), 6.5)), 40, 92);

        var attributes = new PlayerAttributes();
        var bias = RoleBias(position);
        foreach (var name in PlayerAttributes.AllNames)
        {
            var bonus = bias.TryGetValue(name, out var b) ? b : 0;
            var value = baseRating + bonus + rng.Next(-6, 7);
            if (name == "GoalKicking") value = 25 + rng.Next(0, 30);
            attributes.Add(name, value);
        }

        var player = new Player
        {
            FirstName = first,
            LastName = last,
            Position = position,
            Age = age,
            Attributes = attributes,
            Form = rng.Next(50, 76),
            Fitness = 92 + rng.Next(0, 9),
            Sharpness = rng.Next(50, 82),
            Morale = rng.Next(55, 86)
        };

        var swing = attributes.OverallFor(position) - baseRating;
        player.Potential = Math.Clamp(
            player.Rating + (age <= 21 ? rng.Next(4, 16) : age <= 25 ? rng.Next(2, 9) : rng.Next(0, 3)), 45, 97);

        // Fix any drift so club reputation is reflected in the squad's quality.
        _ = swing;

        player.Contract = new Contract
        {
            WeeklyWage = PlayerValue.WeeklyWageFor(player) + rng.Next(-40, 41) * 10,
            YearsRemaining = rng.Next(1, 4),
            SignedSeason = 0
        };
        player.Contract.WeeklyWage = Math.Max(900, player.Contract.WeeklyWage);

        return player;
    }

    /// <summary>A random position, used when an academy graduate is added to a squad.</summary>
    public static PlayerPosition CreateSquadRole(Random rng) => ShirtRoles[rng.Next(ShirtRoles.Length)];

    public static Player CreateFreeAgent(Random rng, int season, HashSet<string> usedNames)
    {
        var position = ShirtRoles[rng.Next(ShirtRoles.Length)];
        var player = CreatePlayer(rng, position, 55 + rng.Next(0, 22), usedNames);
        player.Contract.YearsRemaining = 0;
        player.Contract.SignedSeason = season;
        player.ShirtNumber = 40 + rng.Next(0, 40);
        return player;
    }

    private static int WeightedAge(Random rng)
    {
        // Most players are 21-29, with a tail of teenagers and veterans.
        var roll = rng.NextDouble();
        return roll switch
        {
            < 0.10 => 18 + rng.Next(0, 2),
            < 0.40 => 20 + rng.Next(0, 4),
            < 0.78 => 24 + rng.Next(0, 4),
            < 0.94 => 28 + rng.Next(0, 4),
            _ => 32 + rng.Next(0, 4)
        };
    }

    private static void EnsureGoalKicker(List<Player> squad, Random rng)
    {
        var kicker = squad
            .Where(p => p.Position is PlayerPosition.Fullback or PlayerPosition.Wing or PlayerPosition.Halfback or PlayerPosition.FiveEighth or PlayerPosition.Centre)
            .OrderByDescending(p => p.Attributes.GoalKicking)
            .FirstOrDefault();

        if (kicker is null) return;
        if (kicker.Attributes.GoalKicking < 68)
            kicker.Attributes.GoalKicking = 68 + rng.Next(0, 16);
    }

    public static double Gaussian(Random rng, double mean, double standardDeviation)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + z * standardDeviation;
    }
}
