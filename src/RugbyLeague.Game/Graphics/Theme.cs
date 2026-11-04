using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RugbyLeague.Game.Graphics;

/// <summary>Palette, fonts and textures shared by every screen.</summary>
public sealed class Theme
{
    public SpriteFont Small { get; init; } = null!;
    public SpriteFont Body { get; init; } = null!;
    public SpriteFont Subhead { get; init; } = null!;
    public SpriteFont Heading { get; init; } = null!;
    public SpriteFont Title { get; init; } = null!;

    public Texture2D PanelTexture { get; init; } = null!;
    public Texture2D ButtonTexture { get; init; } = null!;
    public Texture2D BallTexture { get; init; } = null!;
    public Texture2D MarkerTexture { get; init; } = null!;
    public Texture2D ShieldTexture { get; init; } = null!;
    public Texture2D LogoTexture { get; init; } = null!;
    public Texture2D SplashTexture { get; init; } = null!;
    public Texture2D GrassTexture { get; init; } = null!;
    public Texture2D PitchTexture { get; init; } = null!;
    public Texture2D TrophyTexture { get; init; } = null!;
    public Texture2D BackgroundTexture { get; init; } = null!;
    public Texture2D Pixel { get; init; } = null!;

    public static readonly Color Background = new(9, 22, 31);
    public static readonly Color BackgroundDeep = new(6, 15, 22);
    public static readonly Color Panel = new(20, 36, 48);
    public static readonly Color PanelLight = new(28, 48, 62);
    public static readonly Color PanelBorder = new(46, 72, 86);
    public static readonly Color Accent = new(46, 158, 107);
    public static readonly Color AccentDark = new(26, 96, 68);
    public static readonly Color Gold = new(233, 184, 78);
    public static readonly Color Danger = new(205, 69, 59);
    public static readonly Color Text = new(238, 245, 241);
    public static readonly Color TextMuted = new(146, 166, 160);
    public static readonly Color TextDim = new(104, 124, 120);
    public static readonly Color Win = new(72, 186, 118);
    public static readonly Color Loss = new(205, 84, 74);
    public static readonly Color Draw = new(210, 178, 92);
    public static readonly Color AwayBar = new(74, 96, 104);

    public static Color FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Color.White;
        var value = hex.TrimStart('#');
        if (value.Length != 6) return Color.White;

        try
        {
            return new Color(
                Convert.ToInt32(value[..2], 16),
                Convert.ToInt32(value.Substring(2, 2), 16),
                Convert.ToInt32(value.Substring(4, 2), 16));
        }
        catch
        {
            return Color.White;
        }
    }

    public static Color WithAlpha(Color color, float alpha) => color * alpha;

    /// <summary>Picks black or white text for a coloured background.</summary>
    public static Color ContrastingText(Color background)
    {
        var luminance = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255f;
        return luminance > 0.6f ? new Color(16, 24, 28) : Color.White;
    }

    /// <summary>Set from the player's competition: £ in England, $ in Australia.</summary>
    public static string CurrencySymbol { get; set; } = "$";

    public static string Money(long value) =>
        value < 0 ? $"-{CurrencySymbol}{Math.Abs(value):N0}" : $"{CurrencySymbol}{value:N0}";

    public static string ShortMoney(int value) =>
        value >= 1_000_000 ? $"{CurrencySymbol}{value / 1_000_000.0:0.0}M"
        : value >= 1_000 ? $"{CurrencySymbol}{value / 1_000.0:0}K"
        : $"{CurrencySymbol}{value}";
}
