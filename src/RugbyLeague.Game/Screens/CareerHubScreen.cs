using Microsoft.Xna.Framework;
using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Model;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public sealed partial class CareerHubScreen : Screen
{
    private enum HubTab
    {
        Home,
        Squad,
        Tactics,
        Training,
        Transfers,
        Club,
        Competitions
    }

    private readonly Career _career;
    private HubTab _tab = HubTab.Home;
    private float _scroll;

    public CareerHubScreen(RugbyGame game) : base(game)
    {
        _career = game.Career ?? throw new InvalidOperationException("A career must be loaded first.");
    }

    private Team Club => _career.PlayerTeam;
    private League League => _career.PlayerLeague;

    private Rectangle HeaderArea => new(Page.X, Page.Y, Page.Width, 84);

    private Rectangle NavArea => new(Page.X, Page.Y + 96, 200, Page.Height - 96);

    private Rectangle ContentArea =>
        new(Page.X + 216, Page.Y + 96, Page.Width - 216, Page.Height - 96);

    public override void Update(GameTime gameTime)
    {
        if (Input.BackPressed) Game.Screens.Pop();
    }

    public override void Draw()
    {
        DrawCover(Theme.PitchTexture, new Color(26, 38, 38));
        Ui.Fill(View.Bounds, new Color(7, 17, 23, 232));

        DrawHeader();
        DrawNavigation();
        DrawContent();
    }

    private void DrawHeader()
    {
        var header = HeaderArea;
        Ui.Card(header, new Color(15, 30, 40), Theme.PanelBorder);

        var crest = new Rectangle(header.X + 12, header.Y + 10, 64, 64);
        Ui.Fill(crest, Theme.FromHex(Club.PrimaryColor));
        Ui.Border(crest, Theme.FromHex(Club.SecondaryColor), 3);
        Ui.TextIn(Club.ShortName, crest, Theme.ContrastingText(Theme.FromHex(Club.PrimaryColor)),
            TextSize.Subhead, TextAlign.Center);

        Ui.Text(Ui.Ellipsis(Club.Name, TextSize.Body, 560), new Vector2(header.X + 92, header.Y + 12),
            Theme.Text, TextSize.Body);
        Ui.Text(Ui.Ellipsis($"{League.Name}  ·  {_career.ManagerName}  ·  {_career.SeasonYear} season",
                TextSize.Small, 560), new Vector2(header.X + 92, header.Y + 46), Theme.TextMuted, TextSize.Small);

        var right = header.Right - 12;
        if (Ui.Button(new Rectangle(right - 96, header.Y + 20, 96, 44), "Menu", ButtonStyle.Ghost))
        {
            Game.ExitToMenu();
            return;
        }

        right -= 106;
        if (Ui.Button(new Rectangle(right - 96, header.Y + 20, 96, 44), "Save", ButtonStyle.Ghost))
            Game.SaveCareer();

        right -= 116;
        Ui.TextIn($"Round {Math.Min(_career.Round + 1, _career.MaxRounds)} / {_career.MaxRounds}",
            new Rectangle(right - 150, header.Y + 8, 150, 26), Theme.Text, TextSize.Small, TextAlign.Right);
        Ui.TextIn($"Balance {Theme.Money(_career.Balance)}",
            new Rectangle(right - 230, header.Y + 34, 230, 24), Theme.TextMuted, TextSize.Small, TextAlign.Right);
        Ui.TextIn($"Cap space {Theme.Money(_career.CapSpace)}",
            new Rectangle(right - 230, header.Y + 54, 230, 24), Theme.TextDim, TextSize.Small, TextAlign.Right);
    }

    private void DrawNavigation()
    {
        var nav = NavArea;
        Ui.Card(nav, new Color(13, 26, 35), Theme.PanelBorder);

        var items = new (HubTab Tab, string Label)[]
        {
            (HubTab.Home, "Dashboard"),
            (HubTab.Squad, "Squad"),
            (HubTab.Tactics, "Tactics"),
            (HubTab.Training, "Training"),
            (HubTab.Transfers, "Transfers"),
            (HubTab.Club, "Club"),
            (HubTab.Competitions, "Competitions")
        };

        var y = nav.Y + 10;
        foreach (var (tab, label) in items)
        {
            var rectangle = new Rectangle(nav.X + 8, y, nav.Width - 16, 42);
            var badge = tab switch
            {
                HubTab.Transfers => _career.Transfers.PendingOffers.Count(o => o.ToClubId == _career.PlayerTeamId),
                HubTab.Club => _career.SponsorOffers.Count,
                HubTab.Squad => _career.PlayerTeam.Squad.Count(p => p.IsInjured),
                _ => 0
            };

            if (Ui.Tab(rectangle, label, _tab == tab, badge))
            {
                _tab = tab;
                _scroll = 0;
            }

            y += 46;
        }

        var fixture = _career.PendingMatch;
        var buttonY = nav.Bottom - 116;
        var opponent = fixture is null ? "No fixture" : _career.TeamById(
            fixture.Fixture.HomeTeamId == _career.PlayerTeamId ? fixture.Fixture.AwayTeamId : fixture.Fixture.HomeTeamId).ShortName;
        var caption = fixture is null ? "Advance week" : $"Next: {opponent}";

        Ui.TextIn(caption, new Rectangle(nav.X + 10, buttonY - 26, nav.Width - 20, 22), Theme.TextMuted,
            TextSize.Small, TextAlign.Center);

        if (Ui.Button(new Rectangle(nav.X + 10, buttonY, nav.Width - 20, 48),
                fixture is null ? "Advance" : "Matchday", ButtonStyle.Primary))
        {
            if (fixture is null) AdvanceWeek();
            else Game.Screens.Push(new MatchdayScreen(Game));
        }

        if (Ui.Button(new Rectangle(nav.X + 10, buttonY + 56, nav.Width - 20, 40), "Save game", ButtonStyle.Ghost))
            Game.SaveCareer();
    }

    private void AdvanceWeek()
    {
        if (_career.Phase == CompetitionPhase.OffSeason)
        {
            Game.ShowNotice("The season is over. Start a new season from the dashboard.");
            return;
        }

        if (_career.Phase == CompetitionPhase.Regular)
            _career.PlayLeagueRound(null);
        else
            _career.PlayFinalsRound(null);

        Game.SaveCareer();
        Game.ShowNotice("Week advanced; results and training processed.");
        _scroll = 0;
    }

    private void DrawContent()
    {
        var area = ContentArea;
        switch (_tab)
        {
            case HubTab.Home: DrawHome(area); break;
            case HubTab.Squad: DrawSquad(area); break;
            case HubTab.Tactics: DrawTactics(area); break;
            case HubTab.Training: DrawTraining(area); break;
            case HubTab.Transfers: DrawTransfers(area); break;
            case HubTab.Club: DrawClub(area); break;
            case HubTab.Competitions: DrawCompetitions(area); break;
        }
    }

    // ---------------------------------------------------------------- shared pieces

    private void SectionHeader(Rectangle area, string title, string? subtitle = null)
    {
        Ui.Text(title, new Vector2(area.X, area.Y), Theme.Text, TextSize.Subhead);
        if (subtitle is not null)
            Ui.Text(subtitle, new Vector2(area.X, area.Y + 32), Theme.TextDim, TextSize.Small);
    }

    private void StatTile(Rectangle rectangle, string label, string value, Color accent)
    {
        Ui.Card(rectangle, new Color(17, 32, 42), Theme.PanelBorder, shadow: false);
        Ui.Fill(new Rectangle(rectangle.X, rectangle.Y + 10, 3, rectangle.Height - 20), accent);
        Ui.Text(label.ToUpperInvariant(), new Vector2(rectangle.X + 14, rectangle.Y + 10), Theme.TextDim,
            TextSize.Small);
        Ui.Text(value, new Vector2(rectangle.X + 14, rectangle.Y + 34), Theme.Text, TextSize.Subhead);
    }

    private static string PositionLabel(PlayerPosition position) => position switch
    {
        PlayerPosition.Fullback => "FB",
        PlayerPosition.Wing => "WG",
        PlayerPosition.Centre => "CE",
        PlayerPosition.FiveEighth => "SO",
        PlayerPosition.Halfback => "SH",
        PlayerPosition.Prop => "PR",
        PlayerPosition.Hooker => "HK",
        PlayerPosition.SecondRow => "SR",
        PlayerPosition.Lock => "LK",
        _ => position.ToString()[..2].ToUpperInvariant()
    };

    private static Color FormColor(int form) => form >= 70 ? Theme.Win : form >= 45 ? Theme.Draw : Theme.Loss;

    private void PlayerRow(Rectangle rectangle, Player player, bool highlight = false, bool showWage = true)
    {
        Ui.Fill(rectangle, highlight ? new Color(30, 52, 62) : new Color(16, 30, 40));

        var shirtBox = new Rectangle(rectangle.X + 6, rectangle.Y + 6, 34, rectangle.Height - 12);
        Ui.Fill(shirtBox, Theme.BackgroundDeep);
        Ui.TextIn(player.ShirtNumber.ToString(), shirtBox, Theme.TextMuted, TextSize.Small, TextAlign.Center);

        var x = rectangle.X + 50;
        Ui.Text(player.Name, new Vector2(x, rectangle.Y + 6), Theme.Text, TextSize.Small);

        var sub = $"{PositionLabel(player.Position)}  ·  {player.Age}  ·  {player.Attributes.OverallFor(player.Position)} OVR";
        Ui.Text(sub, new Vector2(x, rectangle.Y + 26), Theme.TextMuted, TextSize.Small);

        if (player.IsInjured)
            Ui.Text(player.Injury?.Description ?? "Injured", new Vector2(x + 170, rectangle.Y + 26), Theme.Loss,
                TextSize.Small);

        if (showWage)
            Ui.TextIn(Theme.ShortMoney(player.Contract.WeeklyWage) + "/wk",
                new Rectangle(rectangle.Right - 268, rectangle.Y, 96, rectangle.Height), Theme.TextMuted,
                TextSize.Small, TextAlign.Right);

        var formBar = new Rectangle(rectangle.Right - 156, rectangle.Y + 14, 64, 6);
        Ui.ProgressBar(formBar, player.Form / 99f, FormColor(player.Form));
        Ui.TextIn($"Form {player.Form}", new Rectangle(rectangle.Right - 164, rectangle.Y + 22, 80, 20),
            Theme.TextDim, TextSize.Small, TextAlign.Right);

        var fitBar = new Rectangle(rectangle.Right - 72, rectangle.Y + 14, 56, 6);
        Ui.ProgressBar(fitBar, player.Fitness / 100f, player.Fitness > 70 ? Theme.Accent : Theme.Danger);
        Ui.TextIn($"{player.Fitness}%", new Rectangle(rectangle.Right - 80, rectangle.Y + 22, 64, 20),
            Theme.TextDim, TextSize.Small, TextAlign.Right);
    }
}
