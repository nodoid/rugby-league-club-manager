using RugbyLeague.Core.Model;

namespace RugbyLeague.Core.Careers;

public static class SponsorshipEngine
{
    private static int BaseValue(SponsorTier tier) => tier switch
    {
        SponsorTier.Main => 320_000,
        SponsorTier.Sleeve => 120_000,
        SponsorTier.Shorts => 70_000,
        SponsorTier.StadiumNaming => 260_000,
        SponsorTier.KitSupplier => 95_000,
        _ => 80_000
    };

    public static SponsorDeal CreateInitialDeal(Team team, League league, Random rng, SponsorTier? forcedTier = null)
    {
        var tier = forcedTier ?? (SponsorTier)rng.Next(0, 5);
        var value = ValueFor(team, league, tier, rng);

        return new SponsorDeal
        {
            Brand = TeamDatabaseBrands(rng),
            Tier = tier,
            AnnualValue = value,
            SeasonsRemaining = rng.Next(1, 4),
            BonusPerWin = value / 200,
            BonusPerTrophy = value / 12
        };
    }

    public static SponsorOffer CreateOffer(Team team, League league, Random rng, int round, SponsorTier? forcedTier = null)
    {
        var tier = forcedTier ?? (SponsorTier)rng.Next(0, 5);
        var value = ValueFor(team, league, tier, rng);

        return new SponsorOffer
        {
            Brand = TeamDatabaseBrands(rng),
            Tier = tier,
            AnnualValue = value,
            Length = rng.Next(1, 4),
            BonusPerWin = value / 210,
            BonusPerTrophy = value / 14,
            ExpiresRound = round + 3
        };
    }

    public static int ValueFor(Team team, League league, SponsorTier tier, Random rng)
    {
        var reputation = 0.55 + team.Reputation / 100.0 * 0.95;
        var tierFactor = league.Tier switch { 1 => 1.15, 2 => 0.6, _ => 0.85 };
        var noise = 0.85 + rng.NextDouble() * 0.35;
        return (int)(Math.Round(BaseValue(tier) * reputation * tierFactor * noise / 1000.0) * 1000);
    }

    private static string TeamDatabaseBrands(Random rng)
    {
        var brands = Data.TeamDatabase.SponsorBrands;
        return brands[rng.Next(brands.Length)];
    }
}
