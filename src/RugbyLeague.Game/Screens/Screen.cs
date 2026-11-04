using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.Input;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public abstract class Screen
{
    protected Screen(RugbyGame game)
    {
        Game = game;
    }

    protected RugbyGame Game { get; }
    protected Theme Theme => Game.Theme;
    protected UiContext Ui => Game.Ui;
    protected VirtualViewport View => Game.View;
    protected InputState Input => Game.Input;

    protected Rectangle Page => View.Page();

    /// <summary>Draws a texture cover-scaled to fill the virtual canvas.</summary>
    protected void DrawCover(Texture2D texture, Color? tint = null)
    {
        var scale = Math.Max(View.Width / texture.Width, View.Height / texture.Height);
        var width = (int)(texture.Width * scale) + 1;
        var height = (int)(texture.Height * scale) + 1;
        var destination = new Rectangle((int)((View.Width - width) / 2f), (int)((View.Height - height) / 2f),
            width, height);
        Game.Ui.Batch.Draw(texture, destination, tint ?? Color.White);
    }

    /// <summary>A dark vertical wash used behind menus so text always stays legible.</summary>
    protected void DrawScrim(float leftBias = 0.35f)
    {
        for (var i = 0; i < 24; i++)
        {
            var alpha = (byte)(10 + i * 3);
            Ui.Fill(new Rectangle(0, (int)(View.Height * i / 24f), (int)View.Width,
                (int)(View.Height / 24f) + 1), new Color(5, 12, 18, (int)alpha));
        }

        var panelWidth = (int)(View.Width * leftBias);
        const int strips = 40;
        for (var i = 0; i < strips; i++)
        {
            var alpha = (int)(120 * Math.Pow(1 - i / (float)strips, 2));
            var x = (int)(panelWidth * i / (float)strips);
            var next = (int)(panelWidth * (i + 1) / (float)strips);
            Ui.Fill(new Rectangle(x, 0, next - x + 1, (int)View.Height), new Color(4, 11, 16, alpha));
        }
    }

    protected void Header(string title, string? subtitle = null)
    {
        Ui.Text(title, new Vector2(Page.X, Page.Y), Theme.Text, TextSize.Heading);
        if (!string.IsNullOrWhiteSpace(subtitle))
            Ui.Text(subtitle, new Vector2(Page.X, Page.Y + 56), Theme.TextMuted, TextSize.Small);
    }

    protected bool BackButton(string label = "Back")
    {
        var rectangle = new Rectangle(Page.X, Page.Bottom - 64, 132, 48);
        return Ui.Button(rectangle, label, ButtonStyle.Ghost);
    }

    public virtual void Enter() { }
    public virtual void Leave() { }
    public virtual void Pause() { }
    public virtual void Resume() { }

    public virtual void Update(GameTime gameTime) { }

    /// <summary>Optional 3D pass, drawn before the 2D sprite batch begins.</summary>
    public virtual void DrawWorld(GraphicsDevice device) { }

    public abstract void Draw();
}

public sealed class ScreenManager
{
    private readonly List<Screen> _stack = new();

    public Screen? Current => _stack.Count > 0 ? _stack[^1] : null;

    public int Depth => _stack.Count;

    public void Push(Screen screen)
    {
        Current?.Pause();
        _stack.Add(screen);
        screen.Enter();
    }

    public void Pop()
    {
        if (_stack.Count == 0) return;

        var screen = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        screen.Leave();
        Current?.Resume();
    }

    public void ReplaceAll(Screen screen)
    {
        while (_stack.Count > 0) Pop();
        Push(screen);
    }

    public void Update(GameTime gameTime) => Current?.Update(gameTime);
}
