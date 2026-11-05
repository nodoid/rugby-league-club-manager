using Microsoft.Xna.Framework;
using RugbyLeague.Core.Careers;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public sealed class MainMenuScreen : Screen
{
    private bool _hasSave;

    public MainMenuScreen(RugbyGame game) : base(game)
    {
        _hasSave = game.HasSave;
    }

    public override void Enter()
    {
        _hasSave = Game.HasSave;
        if (_hasSave && Game.Career is null) Game.LoadCareer();
    }

    public override void Update(GameTime gameTime)
    {
        if (Input.BackPressed && Game.IsDesktop)
            Game.Exit();
    }

    public override void Draw()
    {
        DrawCover(Theme.BackgroundTexture);
        Ui.Fill(View.Bounds, new Color(4, 12, 18, 196));
        DrawScrim(0.46f);

        var left = Page.X + 24;
        var logoSize = 104;
        Ui.Batch.Draw(Theme.LogoTexture, new Rectangle(left, Page.Y + 8, logoSize, logoSize), Color.White);

        Ui.Text("RUGBY LEAGUE", new Vector2(left, Page.Y + 120), Theme.Text, TextSize.Title);
        Ui.Text("CLUB MANAGER", new Vector2(left + 4, Page.Y + 204), Theme.Gold, TextSize.Subhead);
        Ui.Text("NRL  ·  NRL CHAMPIONSHIP  ·  TWO ENGLISH DIVISIONS",
            new Vector2(left + 4, Page.Y + 244), Theme.TextMuted, TextSize.Small);

        var buttonWidth = 372;
        var y = 352;
        var x = left;

        if (_hasSave)
        {
            if (Ui.Button(new Rectangle(x, y, buttonWidth, 58), "Continue career", ButtonStyle.Primary))
            {
                if (Game.LoadCareer() && Game.Career is not null)
                    Game.Screens.ReplaceAll(new CareerHubScreen(Game));
                else
                    Game.ShowNotice("The save game could not be loaded.");
            }

            y += 72;
        }

        if (Ui.Button(new Rectangle(x, y, buttonWidth, 58), "New career", ButtonStyle.Gold))
            Game.Screens.Push(new TeamSelectScreen(Game));

        y += 72;

        if (Game.IsDesktop)
        {
            if (Ui.Button(new Rectangle(x, y, buttonWidth, 52), "Quit", ButtonStyle.Ghost))
                Game.Exit();
        }
        else
        {
            Ui.Text("Career progress saves automatically on this device.",
                new Vector2(x, y + 8), Theme.TextDim, TextSize.Small);
        }

        var footer = new Rectangle(left, (int)View.Height - 74, (int)(View.Width - left - 40), 34);
        Ui.TextIn("All clubs, competitions and players are fictional.  ·  " + Game.BuildLabel,
            footer, Theme.TextDim, TextSize.Small);

        if (_hasSave)
            Ui.TextIn(SaveSummary(), new Rectangle(0, (int)View.Height - 106, (int)View.Width - 40, 24),
                Theme.TextMuted, TextSize.Small, TextAlign.Right);
    }

    private string SaveSummary()
    {
        var loaded = Game.Career;
        if (loaded is null) return string.Empty;

        return $"{loaded.PlayerTeam.Name}  ·  {loaded.ManagerName}  ·  {loaded.SeasonYear}";
    }
}
