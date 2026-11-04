namespace RugbyLeague.Core.Model;

public sealed class TeamFinances
{
    /// <summary>Cash at bank. Negative means the board is unhappy.</summary>
    public long Balance { get; set; } = 500_000;

    /// <summary>Season salary cap. The NRL cap is the larger of the two in this game world.</summary>
    public int SalaryCap { get; set; } = 4_400_000;

    public int TicketPrice { get; set; } = 32;
    public int SeasonTicketHolders { get; set; } = 8_000;
    public int WeeklyStadiumCost { get; set; } = 12_000;

    public long SeasonRevenue { get; set; }
    public long SeasonCosts { get; set; }
    public long TransferBudget => Math.Max(0, Balance - 250_000);

    public void ResetSeasonTotals()
    {
        SeasonRevenue = 0;
        SeasonCosts = 0;
    }
}

public sealed class SponsorDeal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Brand { get; set; } = "Sponsor";
    public SponsorTier Tier { get; set; } = SponsorTier.Main;
    public int AnnualValue { get; set; }
    public int SeasonsRemaining { get; set; } = 1;
    public int BonusPerWin { get; set; }
    public int BonusPerTrophy { get; set; }

    public int WeeklyValue => AnnualValue / 52;

    public string TierLabel => Tier switch
    {
        SponsorTier.Main => "Main shirt",
        SponsorTier.Sleeve => "Sleeve",
        SponsorTier.Shorts => "Shorts",
        SponsorTier.StadiumNaming => "Stadium naming",
        SponsorTier.KitSupplier => "Kit supplier",
        _ => Tier.ToString()
    };
}

public sealed class SponsorOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Brand { get; set; } = "Sponsor";
    public SponsorTier Tier { get; set; } = SponsorTier.Sleeve;
    public int AnnualValue { get; set; }
    public int Length { get; set; } = 2;
    public int BonusPerWin { get; set; }
    public int BonusPerTrophy { get; set; }
    public int ExpiresRound { get; set; }

    public string TierLabel => Tier switch
    {
        SponsorTier.Main => "main shirt",
        SponsorTier.Sleeve => "sleeve",
        SponsorTier.Shorts => "shorts",
        SponsorTier.StadiumNaming => "stadium naming",
        SponsorTier.KitSupplier => "kit supplier",
        _ => Tier.ToString().ToLowerInvariant()
    };

    public SponsorDeal ToDeal() => new()
    {
        Brand = Brand,
        Tier = Tier,
        AnnualValue = AnnualValue,
        SeasonsRemaining = Length,
        BonusPerWin = BonusPerWin,
        BonusPerTrophy = BonusPerTrophy
    };
}

public sealed class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Club";
    public string ShortName { get; set; } = "CLB";
    public string Nickname { get; set; } = "Club";
    public string City { get; set; } = "City";
    public string PrimaryColor { get; set; } = "#1F6FEB";
    public string SecondaryColor { get; set; } = "#FFFFFF";
    public string StadiumName { get; set; } = "Ground";
    public int Capacity { get; set; } = 20_000;

    /// <summary>1..99 standing in the game; drives sponsor interest, transfer pull and cup seeding.</summary>
    public int Reputation { get; set; } = 65;

    public Guid LeagueId { get; set; }
    public List<Player> Squad { get; set; } = new();
    public Formation Formation { get; set; } = Formation.Balanced();
    public TacticalSettings Tactics { get; set; } = new();
    public PlayingStyle Style { get; set; } = PlayingStyle.Balanced;
    public TrainingFocus TrainingFocus { get; set; } = TrainingFocus.Balanced;
    public int TrainingFacility { get; set; } = 5;
    public int YouthAcademy { get; set; } = 5;
    public TeamFinances Finances { get; set; } = new();
    public List<SponsorDeal> Sponsors { get; set; } = new();
    public List<Guid> Shortlist { get; set; } = new();

    public string DisplayName => $"{City} {Nickname}";

    public IEnumerable<Player> AvailablePlayers => Squad.Where(p => p.IsAvailable);

    public long WeeklyWageBill => Squad.Sum(p => (long)p.Contract.WeeklyWage);

    public long SalaryCapSpend => Squad.OrderByDescending(p => p.Contract.WeeklyWage).Take(25).Sum(p => (long)p.Contract.WeeklyWage * 52);

    public long AnnualSponsorship => Sponsors.Sum(s => (long)s.AnnualValue);

    public int SquadRating => Squad.Count == 0 ? 0 : (int)Math.Round(Squad.Average(p => p.Rating));

    /// <summary>Best available player for each of the thirteen shirts, by role fit.</summary>
    public TeamSheet SelectBestXV()
    {
        var used = new HashSet<Guid>();
        var starters = new List<Player>();
        var formation = Formation.Slots.OrderBy(s => s.Shirt).ToList();

        foreach (var slot in formation)
        {
            var best = AvailablePlayers
                .Where(p => !used.Contains(p.Id))
                .OrderByDescending(p => p.Attributes.OverallFor(slot.Role) + PositionalFitBonus(p.Position, slot.Role) * 3)
                .ThenByDescending(p => p.Form)
                .FirstOrDefault();

            if (best is null) continue;
            used.Add(best.Id);
            starters.Add(best);
        }

        var bench = AvailablePlayers
            .Where(p => !used.Contains(p.Id))
            .OrderByDescending(p => p.Rating)
            .Take(4)
            .ToList();

        return new TeamSheet
        {
            TeamId = Id,
            TeamName = Name,
            Nickname = Nickname,
            PrimaryColor = PrimaryColor,
            SecondaryColor = SecondaryColor,
            Starters = starters,
            Bench = bench,
            Style = Style,
            Formation = Formation.Clone()
        };
    }

    private static int PositionalFitBonus(PlayerPosition natural, PlayerPosition slot) =>
        natural == slot ? 4 : IsAdjacent(natural, slot) ? 1 : 0;

    private static bool IsAdjacent(PlayerPosition a, PlayerPosition b)
    {
        static bool Forward(PlayerPosition p) =>
            p is PlayerPosition.Prop or PlayerPosition.Hooker or PlayerPosition.SecondRow or PlayerPosition.Lock;

        if (Forward(a) != Forward(b)) return false;
        return Math.Abs((int)a - (int)b) <= 1;
    }
}

/// <summary>A selected seventeen: thirteen starters plus a four-man bench.</summary>
public sealed class TeamSheet
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string PrimaryColor { get; set; } = "#FFFFFF";
    public string SecondaryColor { get; set; } = "#000000";
    public List<Player> Starters { get; set; } = new();
    public List<Player> Bench { get; set; } = new();
    public PlayingStyle Style { get; set; } = PlayingStyle.Balanced;
    public Formation Formation { get; set; } = Formation.Balanced();

    public IEnumerable<Player> All => Starters.Concat(Bench);
    public int Overall => Starters.Count == 0 ? 0 : (int)Math.Round(Starters.Average(p => p.Rating));
}
