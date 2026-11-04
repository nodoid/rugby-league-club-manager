namespace RugbyLeague.Core.Model;

/// <summary>
/// A formation is a set of thirteen relative positions. Coordinates are metres in a frame centred on
/// the play-the-ball: +X points at the opponent's goal line, +Y points to the attacking side's left.
/// Because the frame is relative, any shape a coach can draw can be used by any team, with or
/// without the ball.
/// </summary>
public sealed class FormationSlot
{
    public int Shirt { get; set; }
    public PlayerPosition Role { get; set; }
    public float AttackX { get; set; }
    public float AttackY { get; set; }
    public float DefenceX { get; set; }
    public float DefenceY { get; set; }
}

public sealed class FormationProfile
{
    /// <summary>How wide the attacking line is stretched (metres).</summary>
    public float AttackWidth { get; set; }
    /// <summary>How deep the attacking line sits behind the play-the-ball.</summary>
    public float AttackDepth { get; set; }
    /// <summary>How flat and aggressive the defensive line is; higher means up fast.</summary>
    public float DefenceLine { get; set; }
    /// <summary>Concentration of bodies in the middle third of the field, 0..1.</summary>
    public float MiddleConcentration { get; set; }
    /// <summary>Spread of the defensive line, 0..1.</summary>
    public float DefenceWidth { get; set; }
}

public sealed class Formation
{
    public string Name { get; set; } = "Balanced";
    public List<FormationSlot> Slots { get; set; } = new();

    public Formation Clone() => new()
    {
        Name = Name,
        Slots = Slots.Select(s => new FormationSlot
        {
            Shirt = s.Shirt,
            Role = s.Role,
            AttackX = s.AttackX,
            AttackY = s.AttackY,
            DefenceX = s.DefenceX,
            DefenceY = s.DefenceY
        }).ToList()
    };

    public FormationSlot? SlotForShirt(int shirt) => Slots.FirstOrDefault(s => s.Shirt == shirt);
    public int SlotCount => Slots.Count;

    /// <summary>A formation is legal when it has thirteen unique shirts 1..13 and every slot has a role.</summary>
    public bool IsValid(out string error)
    {
        if (Slots.Count != 13)
        {
            error = $"A formation needs exactly 13 shirts (found {Slots.Count}).";
            return false;
        }

        if (Slots.Select(s => s.Shirt).Distinct().Count() != 13 || Slots.Any(s => s.Shirt is < 1 or > 13))
        {
            error = "Shirts must be unique numbers from 1 to 13.";
            return false;
        }

        if (Slots.Any(s => Math.Abs(s.AttackX) > 40 || Math.Abs(s.AttackY) > 34 ||
                           Math.Abs(s.DefenceX) > 40 || Math.Abs(s.DefenceY) > 34))
        {
            error = "Positions must stay inside the pitch.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public FormationProfile Profile()
    {
        if (Slots.Count == 0)
            return new FormationProfile();

        var attackWidth = Slots.Average(s => Math.Abs(s.AttackY)) / 34f;
        var attackDepth = Slots.Average(s => s.AttackX);
        var defenceLine = Slots.Average(s => s.DefenceX);
        var middle = Slots.Count(s => Math.Abs(s.AttackY) < 12) / (float)Slots.Count;
        var defenceWidth = Slots.Average(s => Math.Abs(s.DefenceY)) / 34f;

        return new FormationProfile
        {
            AttackWidth = Math.Clamp(attackWidth, 0f, 1f),
            AttackDepth = attackDepth,
            DefenceLine = defenceLine,
            MiddleConcentration = middle,
            DefenceWidth = Math.Clamp(defenceWidth, 0f, 1f)
        };
    }

    /// <summary>Attack multiplier granted by the shape, centred on 1.0.</summary>
    public float EdgeThreat => 0.94f + Profile().AttackWidth * 0.14f;

    /// <summary>Middle-third go-forward multiplier.</summary>
    public float MiddleThreat => 0.96f + Profile().MiddleConcentration * 0.12f;

    /// <summary>Defensive line speed multiplier; aggressive lines win the ruck but leak edges.</summary>
    public float LineSpeed => 0.97f + Math.Clamp(Profile().DefenceLine / 20f, 0f, 1f) * 0.1f;

    public static IReadOnlyList<Formation> Presets() => new List<Formation>
    {
        Balanced(),
        WideSpread(),
        TightPack(),
        SplitHalves(),
        FlatDefence()
    };

    /// <summary>The default shape every club starts with.</summary>
    public static Formation Balanced()
    {
        var slots = new List<FormationSlot>();
        void Add(int shirt, PlayerPosition role, float ax, float ay, float dx, float dy) =>
            slots.Add(new FormationSlot { Shirt = shirt, Role = role, AttackX = ax, AttackY = ay, DefenceX = dx, DefenceY = dy });

        // Attack offsets sit behind the play-the-ball (negative X); defensive offsets sit up
        // the field from it (positive X) because a defensive line retreats ten metres.
        Add(1, PlayerPosition.Fullback, -16, 0, -10, 0);
        Add(2, PlayerPosition.Wing, -5, 26, 11, 26);
        Add(3, PlayerPosition.Centre, -3, 16, 10, 15);
        Add(4, PlayerPosition.Centre, -3, -16, 10, -15);
        Add(5, PlayerPosition.Wing, -5, -26, 11, -26);
        Add(6, PlayerPosition.FiveEighth, -1, 9, 11, 8);
        Add(7, PlayerPosition.Halfback, -4, -8, 11, -8);
        Add(8, PlayerPosition.Prop, -2, 5, 12, 5);
        Add(9, PlayerPosition.Hooker, 0, 0, 12, 0);
        Add(10, PlayerPosition.Prop, -2, -5, 12, -5);
        Add(11, PlayerPosition.SecondRow, -4, 11, 12, 11);
        Add(12, PlayerPosition.SecondRow, -4, -11, 12, -11);
        Add(13, PlayerPosition.Lock, -5, 0, 12, 0);

        return new Formation { Name = "Balanced", Slots = slots };
    }

    public static Formation WideSpread()
    {
        var f = Balanced();
        f.Name = "Wide Spread";
        foreach (var s in f.Slots)
        {
            if (s.Role is PlayerPosition.Wing or PlayerPosition.Centre)
            {
                s.AttackY *= 1.25f;
                s.DefenceY *= 1.2f;
            }
            else if (s.Role is PlayerPosition.FiveEighth or PlayerPosition.Halfback)
            {
                s.AttackX += 3;
            }
            else if (s.Role == PlayerPosition.Fullback)
            {
                s.AttackX -= 6;
                s.AttackY += 6;
            }
        }
        return f;
    }

    public static Formation TightPack()
    {
        var f = Balanced();
        f.Name = "Tight Pack";
        foreach (var s in f.Slots)
        {
            if (s.Role is PlayerPosition.Prop or PlayerPosition.SecondRow or PlayerPosition.Lock or PlayerPosition.Hooker)
            {
                s.AttackY *= 0.65f;
                s.AttackX += 1;
                s.DefenceY *= 0.8f;
            }
            if (s.Role is PlayerPosition.Wing or PlayerPosition.Centre)
                s.AttackX -= 3;
        }
        return f;
    }

    public static Formation SplitHalves()
    {
        var f = Balanced();
        f.Name = "Split Halves";
        foreach (var s in f.Slots)
        {
            if (s.Role == PlayerPosition.FiveEighth)
            {
                s.AttackX += 5;
                s.AttackY -= 12;
            }
            else if (s.Role == PlayerPosition.Halfback)
            {
                s.AttackX += 5;
                s.AttackY += 12;
            }
            else if (s.Role == PlayerPosition.Fullback)
            {
                s.AttackX += 4;
                s.AttackY -= 4;
            }
        }
        return f;
    }

    public static Formation FlatDefence()
    {
        var f = Balanced();
        f.Name = "Flat Defence";
        foreach (var s in f.Slots)
        {
            s.DefenceX += 4;
            s.DefenceY *= 1.05f;
        }
        return f;
    }
}
