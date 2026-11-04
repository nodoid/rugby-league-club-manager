using Microsoft.Xna.Framework;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public sealed class SplashScreen : Screen
{
    private const float MinimumTime = 1.4f;
    private float _time;

    public SplashScreen(RugbyGame game) : base(game) { }

    public override void Update(GameTime gameTime)
    {
        _time += (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_time >= 2.1f || (Input.PointerPressed && _time > MinimumTime))
            Game.Screens.ReplaceAll(new MainMenuScreen(Game));
    }

    public override void Draw()
    {
        DrawCover(Theme.SplashTexture);

        var fadeIn = Math.Clamp(_time / 0.5f, 0f, 1f);
        Ui.Fill(View.Bounds, Color.Black * (1f - fadeIn) * 0.8f);

        var bar = new Rectangle((int)(View.Width / 2 - 110), (int)(View.Height - 58), 220, 4);
        Ui.Fill(bar, new Color(255, 255, 255, 40));
        Ui.Fill(new Rectangle(bar.X, bar.Y, (int)(bar.Width * Math.Clamp(_time / 2.1f, 0f, 1f)), bar.Height),
            Theme.Gold);

        Ui.TextIn("Tap to continue", new Rectangle(0, (int)(View.Height - 44), (int)View.Width, 24),
            Theme.TextDim * fadeIn, TextSize.Small, TextAlign.Center);
    }
}
