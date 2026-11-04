using RugbyLeague.Core.Model;

namespace RugbyLeague.Core.Data;

/// <summary>
/// The starting world. Every club is an original invention that borrows the flavour of a
/// real-world region, but no name, nickname, badge or sponsor here is a real one.
/// </summary>
public static class TeamDatabase
{
    public sealed record ClubSeed(
        string City,
        string Nickname,
        string ShortName,
        string Primary,
        string Secondary,
        string Stadium,
        int Capacity,
        int Reputation,
        PlayingStyle Style);

    public static readonly Guid NrlId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SuperLeagueId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid ChampionshipId = new("33333333-3333-3333-3333-333333333333");

    public static readonly ClubSeed[] NrlClubs =
    {
        new("Brisbane", "Stallions", "BRI", "#6D1B2E", "#F0C24B", "Rivergate Stadium", 52_500, 90, PlayingStyle.ForwardPower),
        new("Sydney", "Cockerels", "SYD", "#13294B", "#E03A3E", "Moore Park Oval", 45_500, 84, PlayingStyle.Balanced),
        new("Melbourne", "Cyclones", "MEL", "#3B1361", "#9D7BD8", "Docklands Arena", 30_050, 92, PlayingStyle.Expansive),
        new("Penrith", "Pumas", "PEN", "#0B3D2E", "#E8D9A0", "Blue Mountains Park", 22_500, 88, PlayingStyle.ForwardPower),
        new("Parramatta", "Rays", "PAR", "#0A3D62", "#F5C518", "Wentworth Field", 30_000, 76, PlayingStyle.Balanced),
        new("Canterbury", "Bulls", "CBY", "#0B3C8C", "#FFFFFF", "Belmore Ground", 20_000, 72, PlayingStyle.ForwardPower),
        new("Sutherland", "Marlins", "SUT", "#0E6E6E", "#111111", "Southern Cross Oval", 18_500, 74, PlayingStyle.Balanced),
        new("Manly", "Kites", "MAN", "#6B1D5E", "#F2F2F2", "Northern Beaches Park", 18_000, 78, PlayingStyle.Expansive),
        new("South Sydney", "Hares", "SOU", "#1B3A2B", "#C8102E", "Redfern Bowl", 20_500, 80, PlayingStyle.Balanced),
        new("Illawarra", "Wyverns", "ILL", "#B3121B", "#FFFFFF", "Steelworks Oval", 22_000, 75, PlayingStyle.Balanced),
        new("Newcastle", "Paladins", "NEW", "#1E4D8C", "#D22B2B", "Hunter Stadium North", 30_000, 73, PlayingStyle.KickingGame),
        new("Canberra", "Marauders", "CAN", "#0F5B3E", "#E4B429", "Capital Oval", 25_000, 77, PlayingStyle.ForwardPower),
        new("Gold Coast", "Colossi", "GLD", "#0A7FA0", "#F7D54A", "Seabreeze Stadium", 27_500, 70, PlayingStyle.Expansive),
        new("Townsville", "Drovers", "TWN", "#1F3F7A", "#F2C230", "Cattleman's Park", 25_000, 79, PlayingStyle.ForwardPower),
        new("Inner West", "Cougars", "IWC", "#D2691E", "#111111", "Leichhardt Field", 17_500, 68, PlayingStyle.Balanced),
        new("Auckland", "Voyagers", "AKL", "#0F4C81", "#C0C0C0", "Waitemata Arena", 25_000, 76, PlayingStyle.Expansive),
        new("Moreton Bay", "Porpoises", "MBP", "#12776C", "#F5F5DC", "Redcliffe Bayside", 11_500, 66, PlayingStyle.Balanced)
    };

    public static readonly ClubSeed[] SuperLeagueClubs =
    {
        new("Wigan", "Legion", "WIG", "#8B1A1A", "#FFFFFF", "Central Park North", 25_000, 88, PlayingStyle.ForwardPower),
        new("St Helens", "Vulcans", "STH", "#0B2C6B", "#D4AF37", "Windle Valley", 18_000, 87, PlayingStyle.Expansive),
        new("Leeds", "Mammoths", "LEE", "#0B3D91", "#F5B301", "Headingley Ridge", 21_000, 85, PlayingStyle.Balanced),
        new("Warrington", "Coyotes", "WAR", "#0F4C3A", "#F0E68C", "Rylands Park", 15_200, 80, PlayingStyle.Balanced),
        new("Hull", "Dockers", "HUD", "#8C1D40", "#F2F2F2", "East Docks Ground", 12_200, 78, PlayingStyle.Balanced),
        new("Hull", "Trawlers", "HUT", "#111111", "#E8B923", "West Harbour Stadium", 25_500, 77, PlayingStyle.KickingGame),
        new("Catalan", "Suns", "CAT", "#C8102E", "#F7C948", "Perpignan Sud", 12_000, 79, PlayingStyle.Expansive),
        new("Leigh", "Lynx", "LEI", "#1B4D3E", "#E4D00A", "Atherton Road", 12_000, 72, PlayingStyle.ForwardPower),
        new("Salford", "Imps", "SAL", "#B31B1B", "#F5F5F5", "Willow Lane", 11_000, 70, PlayingStyle.Balanced),
        new("Huddersfield", "Engineers", "HUD", "#1C3F94", "#FFFFFF", "Kirklees Park", 24_500, 68, PlayingStyle.Attritional),
        new("Castleford", "Jaguars", "CAS", "#D4A017", "#111111", "Wheldon Road", 12_000, 69, PlayingStyle.Balanced),
        new("Wakefield", "Abbeys", "WAK", "#1E6B52", "#E6E6E6", "Trinity Walk", 9_500, 64, PlayingStyle.KickingGame)
    };

    public static readonly ClubSeed[] ChampionshipClubs =
    {
        new("Bradford", "Aurochs", "BRA", "#8B2500", "#F5E6C8", "Odsal Rise", 26_000, 63, PlayingStyle.ForwardPower),
        new("Toulouse", "Aviators", "TOU", "#1B3C8C", "#F2C230", "Garonne Stadium", 19_500, 62, PlayingStyle.Expansive),
        new("Featherstone", "Forgers", "FEA", "#1C1C1C", "#D4AF37", "Post Office Road", 8_500, 58, PlayingStyle.Balanced),
        new("York", "Constables", "YOR", "#0F3D5C", "#F0B429", "Bootham Crescent", 8_500, 57, PlayingStyle.Balanced),
        new("London", "Meteors", "LON", "#111111", "#E03A3E", "Thames Dockside", 12_500, 60, PlayingStyle.Expansive),
        new("Widnes", "Longships", "WID", "#1B3C6B", "#E6E6E6", "Halton Brow", 13_000, 59, PlayingStyle.KickingGame),
        new("Halifax", "Ravens", "HAL", "#2B2B6B", "#F0F0F0", "Calder Vale", 10_500, 58, PlayingStyle.Balanced),
        new("Barrow", "Buccaneers", "BAR", "#0F5B6E", "#F5D76E", "Docklands Reach", 7_000, 54, PlayingStyle.Attritional),
        new("Sheffield", "Falcons", "SHE", "#3C8D2F", "#F2F2F2", "Don Valley Bowl", 9_500, 56, PlayingStyle.Expansive),
        new("Batley", "Beagles", "BAT", "#6B1D5E", "#F5F5DC", "Mount Pleasant", 7_500, 52, PlayingStyle.ForwardPower),
        new("Dewsbury", "Tups", "DEW", "#7A1F1F", "#E8E8E8", "Crown Flatt", 7_500, 51, PlayingStyle.Attritional),
        new("Whitehaven", "Herders", "WHI", "#1F4E5F", "#D9D9D9", "The Recreation Ground", 7_500, 50, PlayingStyle.ForwardPower)
    };

    public static readonly string[] SponsorBrands =
    {
        "Kangaroo Energy", "Southern Cross Assurance", "Steelworks Brewing", "Harbourline Logistics",
        "Trakwell Systems", "Ozone Fitness", "Pennant Bank", "Granite Motors", "Blue Ridge Water",
        "Ironbark Timber", "Metrogas Utilities", "Civic Telecom", "Vantage Sportswear", "Cutwater Marine",
        "Outback Air", "Ravenhill Foods", "Kestrel Pay", "Northgate Tyres", "Solstice Solar", "Tidewater Homes"
    };

    public static IReadOnlyList<League> BuildLeagues()
    {
        return new List<League>
        {
            new()
            {
                Id = NrlId,
                Name = "National Rugby League",
                ShortName = "NRL",
                Tier = 1,
                Country = "Australia",
                HasPromotionRelegation = false,
                FinalsSpots = 8,
                ByePoints = 2,
                SalaryCap = 4_600_000,
                TvMoney = 9_500_000,
                PrizeMoney = 1_200_000
            },
            new()
            {
                Id = SuperLeagueId,
                Name = "Super League",
                ShortName = "SL",
                Tier = 1,
                Country = "England",
                HasPromotionRelegation = true,
                PromotionSpots = 1,
                RelegationSpots = 1,
                FinalsSpots = 6,
                ByePoints = 0,
                SalaryCap = 3_100_000,
                TvMoney = 4_200_000,
                PrizeMoney = 750_000
            },
            new()
            {
                Id = ChampionshipId,
                Name = "The Championship",
                ShortName = "CHM",
                Tier = 2,
                Country = "England",
                HasPromotionRelegation = true,
                PromotionSpots = 1,
                RelegationSpots = 0,
                FinalsSpots = 4,
                ByePoints = 0,
                SalaryCap = 1_500_000,
                TvMoney = 650_000,
                PrizeMoney = 250_000
            }
        };
    }
}
