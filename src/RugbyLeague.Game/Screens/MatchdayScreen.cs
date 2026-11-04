using Microsoft.Xna.Framework;
using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Model;
using RugbyLeague.Core.Sim;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public sealed class MatchdayScreen : Screen
{
    private readonly Career _career;
    private MatchSetup? _setup;
    private MatchResult? _quickResult;

    public MatchdayScreen(RugbyGame game) : base(game)
    {
        _career = game.Career ?? throw new InvalidOperationException("No career loaded.");
    }

    public override void Enter()
    {
        _setup ??= _career.PendingSetup(true);
    }

    public override void Update(GameTime gameTime)
    {
        if (Input.BackPressed) Game.Screens.Pop();
    }

    public override void Draw()
    {
        DrawCover(Theme.PitchTexture, new Color(22, 34, 34));
        Ui.Fill(View.Bounds, new Color(6, 16, 22, 234));

        var area = Page;
        if (Ui.Button(new Rectangle(area.X, area.Y, 120, 40), "Back", ButtonStyle.Ghost))
        {
            Game.Screens.Pop();
            return;
        }

        var pending = _career.PendingMatch;
        if (pending is null || _setup is null)
        {
            Ui.Text("No fixture is scheduled.", new Vector2(area.X, area.Y + 70), Theme.TextMuted, TextSize.Body);
            return;
        }

        var home = _setup.Home;
        var away = _setup.Away;
        var playerIsHome = home.TeamId == _career.PlayerTeamId;

        Ui.Text(pending.CompetitionName, new Vector2(area.X + 136, area.Y + 4), Theme.Gold, TextSize.Subhead);
        Ui.Text($"Round {pending.Fixture.Round + 1}  ·  {_setup.Weather.Label}  ·  {_setup.Weather.Temperature}°C",
            new Vector2(area.X + 136, area.Y + 38), Theme.TextMuted, TextSize.Small);

        var card = new Rectangle(area.X, area.Y + 66, area.Width, area.Height - 66);
        Ui.Card(card, new Color(14, 28, 38), Theme.PanelBorder);

        var scoreboard = new Rectangle(card.X + 20, card.Y + 18, card.Width - 40, 120);
        Ui.Fill(scoreboard, new Color(10, 22, 30));

        DrawTeamBadge(new Rectangle(scoreboard.X + 30, scoreboard.Y + 20, 80, 80), home);
        Ui.TextIn(home.TeamName, new Rectangle(scoreboard.X + 124, scoreboard.Y + 18, 360, 40), Theme.Text,
            TextSize.Subhead, TextAlign.Left);
        Ui.TextIn($"Squad rating {home.Overall}  ·  {(playerIsHome ? "you" : "opponent")}",
            new Rectangle(scoreboard.X + 124, scoreboard.Y + 60, 360, 24), Theme.TextMuted, TextSize.Small,
            TextAlign.Left);

        Ui.TextIn("VS", new Rectangle(scoreboard.Center.X - 40, scoreboard.Y + 44, 80, 30), Theme.Gold, TextSize.Subhead,
            TextAlign.Center);

        DrawTeamBadge(new Rectangle(scoreboard.Right - 110, scoreboard.Y + 20, 80, 80), away);
        Ui.TextIn(away.TeamName, new Rectangle(scoreboard.Right - 484, scoreboard.Y + 18, 360, 40), Theme.Text,
            TextSize.Subhead, TextAlign.Right);
        Ui.TextIn($"Squad rating {away.Overall}  ·  {(playerIsHome ? "opponent" : "you")}",
            new Rectangle(scoreboard.Right - 484, scoreboard.Y + 60, 360, 24), Theme.TextMuted, TextSize.Small,
            TextAlign.Right);

        var sheetsY = card.Y + 156;
        var sheetWidth = (card.Width - 60) / 2;
        DrawSheet(new Rectangle(card.X + 20, sheetsY, sheetWidth, card.Height - 156 - 96), home, "HOME XIII");
        DrawSheet(new Rectangle(card.X + 40 + sheetWidth, sheetsY, sheetWidth, card.Height - 156 - 96), away,
            "AWAY XIII");

        var buttonY = card.Bottom - 78;
        if (Ui.Button(new Rectangle(card.X + 20, buttonY, 250, 56), "Play 3D match", ButtonStyle.Primary))
            Game.Screens.Push(new ThreeDMatchScreen(Game, _setup, _career));

        if (Ui.Button(new Rectangle(card.X + 282, buttonY, 250, 56), "Watch simulation", ButtonStyle.Gold))
            Game.Screens.Push(new TopDownMatchScreen(Game, _setup, _career));

        if (Ui.Button(new Rectangle(card.X + 544, buttonY, 220, 56), "Quick result", ButtonStyle.Ghost))
        {
            var quickSetup = new MatchSetup
            {
                Home = _setup.Home,
                Away = _setup.Away,
                Weather = _setup.Weather,
                Seed = _setup.Seed,
                CompetitionName = _setup.CompetitionName,
                MustHaveWinner = _setup.MustHaveWinner,
                HomeTactics = _setup.HomeTactics,
                AwayTactics = _setup.AwayTactics,
                CaptureFrames = false
            };

            _quickResult = MatchEngine.Simulate(quickSetup);
            CompleteQuickly();
        }

        Ui.TextIn("3D mode is arcade rugby: carry, pass and kick with the on-screen controls.",
            new Rectangle(card.X + 20, buttonY - 30, card.Width - 40, 24), Theme.TextDim, TextSize.Small,
            TextAlign.Right);
    }

    private void CompleteQuickly()
    {
        if (_quickResult is null) return;

        var round = _career.Round;
        _career.SubmitPendingMatch(_quickResult);
        Game.SaveCareer();
        Game.Screens.ReplaceAll(new PostMatchScreen(Game, _quickResult, round));
    }

    private void DrawTeamBadge(Rectangle rectangle, TeamSheet sheet)
    {
        var primary = Theme.FromHex(sheet.PrimaryColor);
        Ui.Fill(rectangle, primary);
        Ui.Border(rectangle, Theme.FromHex(sheet.SecondaryColor), 4);
        Ui.TextIn(sheet.Nickname.Length > 3 ? sheet.Nickname[..3].ToUpperInvariant() : sheet.Nickname.ToUpperInvariant(),
            rectangle, Theme.ContrastingText(primary), TextSize.Subhead, TextAlign.Center);
    }

    private static string PositionCode(PlayerPosition position) => position switch
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
        _ => "??"
    };

    private void DrawSheet(Rectangle area, TeamSheet sheet, string caption)
    {
        Ui.Fill(area, new Color(17, 32, 42));
        Ui.Text(caption, new Vector2(area.X + 14, area.Y + 10), Theme.TextDim, TextSize.Small);

        Ui.TextIn(sheet.Formation.Name, new Rectangle(area.Right - 240, area.Y + 6, 220, 24), Theme.TextMuted,
            TextSize.Small, TextAlign.Right);

        var y = area.Y + 34;
        var rowHeight = Math.Max(22, (area.Height - 44) / 14);

        foreach (var player in sheet.Starters.Take(13))
        {
            var row = new Rectangle(area.X + 8, y, area.Width - 16, rowHeight - 2);
            if (player.IsInjured) Ui.Fill(row, new Color(44, 26, 26));

            Ui.Text($"{player.ShirtNumber,2}", new Vector2(row.X + 6, row.Y + 2), Theme.TextDim, TextSize.Small);
            Ui.Text(Ui.Ellipsis(player.ShortName, TextSize.Small, row.Width - 190),
                new Vector2(row.X + 34, row.Y + 2), Theme.Text, TextSize.Small);
            Ui.TextIn($"{player.Rating}", new Rectangle(row.Right - 168, row.Y + 2, 54, rowHeight),
                Theme.TextMuted, TextSize.Small, TextAlign.Right);
            Ui.TextIn(PositionCode(player.Position), new Rectangle(row.Right - 96, row.Y + 2, 82, rowHeight),
                Theme.TextDim, TextSize.Small, TextAlign.Right);

            y += rowHeight;
        }

        if (y + 20 < area.Bottom)
            Ui.Text("Bench: " + string.Join(", ", sheet.Bench.Select(p => p.ShortName)),
                new Vector2(area.X + 12, area.Bottom - 26), Theme.TextDim, TextSize.Small);
    }
}
