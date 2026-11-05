using Microsoft.Xna.Framework;

namespace RugbyLeague.Game.Graphics;

/// <summary>
/// Letterbox-free virtual canvas. The design height is 720 units; the width grows on wider screens
/// so phones get the full display instead of black bars. All UI code works in these units.
/// </summary>
public sealed class VirtualViewport
{
    public const float DesignHeight = 720f;
    public const float MinimumWidth = 960f;

    public float Scale { get; private set; } = 1f;
    public float Width { get; private set; } = 1280f;
    public float Height { get; private set; } = DesignHeight;
    public Matrix Transform { get; private set; } = Matrix.Identity;

    public int ScreenWidth { get; private set; }
    public int ScreenHeight { get; private set; }

    public void Update(int screenWidth, int screenHeight)
    {
        if (screenWidth <= 0 || screenHeight <= 0) return;

        ScreenWidth = screenWidth;
        ScreenHeight = screenHeight;

        // Fit the design canvas inside the window. Wide screens grow the width, but a screen that is
        // too square to hold the 960x720 minimum (iPad windows, for example) shrinks the scale and
        // letterboxes instead of drawing the right-hand side off the edge.
        Scale = Math.Min(screenHeight / DesignHeight, screenWidth / MinimumWidth);
        Width = Math.Max(MinimumWidth, screenWidth / Scale);
        Height = DesignHeight;

        var offsetX = (screenWidth - Width * Scale) / 2f;
        var offsetY = (screenHeight - Height * Scale) / 2f;
        Transform = Matrix.CreateScale(Scale, Scale, 1f) * Matrix.CreateTranslation(offsetX, offsetY, 0);
    }

    public Vector2 ToVirtual(Vector2 screenPosition) => screenPosition / Scale;

    public Rectangle Bounds => new(0, 0, (int)Width, (int)Height);

    /// <summary>A rectangle inset from the screen edges; the standard page frame.</summary>
    public Rectangle Page(float margin = 28f) =>
        new((int)margin, (int)margin, (int)(Width - margin * 2), (int)(Height - margin * 2));
}
