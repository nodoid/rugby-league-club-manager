using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.Input;

namespace RugbyLeague.Game.UI;

public enum ButtonStyle
{
    Normal,
    Primary,
    Danger,
    Ghost,
    Gold
}

public enum TextAlign
{
    Left,
    Center,
    Right
}

/// <summary>
/// Immediate-mode UI tuned for a finger: every control reports a tap on release, so a drag that
/// starts on a button and scrolls a list never fires the button by accident.
/// </summary>
public sealed class UiContext
{
    private Vector2 _pressPosition;
    private bool _tapValid;

    public Theme Theme { get; }
    public SpriteBatch Batch { get; }
    public InputState Input { get; }
    public VirtualViewport View { get; }

    public UiContext(Theme theme, SpriteBatch batch, InputState input, VirtualViewport view)
    {
        Theme = theme;
        Batch = batch;
        Input = input;
        View = view;
    }

    public void Update()
    {
        if (Input.PointerPressed)
        {
            _pressPosition = Input.Pointer;
            _tapValid = true;
        }

        if (Input.PointerDown && Vector2.DistanceSquared(_pressPosition, Input.Pointer) > 22f * 22f)
            _tapValid = false;

        if (Input.PointerReleased && _tapValid) _tapValid = true;
    }

    public bool Tapped(Rectangle area) =>
        Input.PointerReleased && _tapValid && area.Contains(Input.Pointer.ToPoint());

    public bool Pressed(Rectangle area) =>
        Input.PointerDown && Vector2.DistanceSquared(_pressPosition, Input.Pointer) < 18f * 18f &&
        area.Contains(Input.Pointer.ToPoint());

    // ---------------------------------------------------------------- primitives

    public void Fill(Rectangle rectangle, Color color) =>
        Batch.Draw(Theme.Pixel, rectangle, color);

    public void Border(Rectangle rectangle, Color color, int thickness = 1)
    {
        Fill(new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        Fill(new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        Fill(new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        Fill(new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }

    public void NineSlice(Rectangle destination, Color color, Texture2D? texture = null)
    {
        texture ??= Theme.PanelTexture;
        const int corner = 24;
        var sourceSize = texture.Width;
        var c = Math.Min(corner, Math.Min(destination.Width, destination.Height) / 2);

        if (c <= 0)
        {
            Fill(destination, color);
            return;
        }

        var left = c;
        var right = destination.Width - c;
        var top = c;
        var bottom = destination.Height - c;

        Rectangle Source(int x, int y, int w, int h) => new(x, y, w, h);

        // corners
        Batch.Draw(texture, new Rectangle(destination.X, destination.Y, c, c), Source(0, 0, corner, corner), color);
        Batch.Draw(texture, new Rectangle(destination.Right - c, destination.Y, c, c),
            Source(sourceSize - corner, 0, corner, corner), color);
        Batch.Draw(texture, new Rectangle(destination.X, destination.Bottom - c, c, c),
            Source(0, sourceSize - corner, corner, corner), color);
        Batch.Draw(texture, new Rectangle(destination.Right - c, destination.Bottom - c, c, c),
            Source(sourceSize - corner, sourceSize - corner, corner, corner), color);

        // edges
        Batch.Draw(texture, new Rectangle(destination.X + c, destination.Y, right - left, c),
            Source(corner, 0, sourceSize - corner * 2, corner), color);
        Batch.Draw(texture, new Rectangle(destination.X + c, destination.Bottom - c, right - left, c),
            Source(corner, sourceSize - corner, sourceSize - corner * 2, corner), color);
        Batch.Draw(texture, new Rectangle(destination.X, destination.Y + c, c, bottom - top),
            Source(0, corner, corner, sourceSize - corner * 2), color);
        Batch.Draw(texture, new Rectangle(destination.Right - c, destination.Y + c, c, bottom - top),
            Source(sourceSize - corner, corner, corner, sourceSize - corner * 2), color);

        // centre
        Batch.Draw(texture, new Rectangle(destination.X + c, destination.Y + c, right - left, bottom - top),
            Source(corner, corner, sourceSize - corner * 2, sourceSize - corner * 2), color);
    }

    public void Card(Rectangle rectangle, Color? fill = null, Color? border = null, bool shadow = true)
    {
        if (shadow)
            NineSlice(new Rectangle(rectangle.X + 4, rectangle.Y + 5, rectangle.Width, rectangle.Height),
                new Color(0, 0, 0, 90));

        NineSlice(rectangle, fill ?? Theme.Panel);
        if (border is { } borderColor) Border(rectangle, borderColor);
    }

    // ---------------------------------------------------------------- text

    public SpriteFont FontFor(TextSize size) => size switch
    {
        TextSize.Small => Theme.Small,
        TextSize.Body => Theme.Body,
        TextSize.Subhead => Theme.Subhead,
        TextSize.Heading => Theme.Heading,
        TextSize.Title => Theme.Title,
        _ => Theme.Body
    };

    public void Text(string text, Vector2 position, Color color, TextSize size = TextSize.Body,
        TextAlign align = TextAlign.Left)
    {
        var font = FontFor(size);
        text = Sanitise(text, font);
        var measured = font.MeasureString(text);

        var offset = align switch
        {
            TextAlign.Center => new Vector2(-measured.X / 2, 0),
            TextAlign.Right => new Vector2(-measured.X, 0),
            _ => Vector2.Zero
        };

        Batch.DrawString(font, text, position + offset, color);
    }

    public void TextIn(string text, Rectangle area, Color color, TextSize size = TextSize.Body,
        TextAlign align = TextAlign.Left)
    {
        var font = FontFor(size);
        text = Sanitise(text, font);
        var measured = font.MeasureString(text);

        var x = align switch
        {
            TextAlign.Center => area.X + (area.Width - measured.X) / 2f,
            TextAlign.Right => area.Right - measured.X,
            _ => area.X
        };
        var y = area.Y + (area.Height - measured.Y) / 2f;

        Batch.DrawString(font, text, new Vector2(MathF.Round(x), MathF.Round(y)), color);
    }

    public void WrappedText(string text, Rectangle area, Color color, TextSize size = TextSize.Body,
        float lineSpacing = 4f)
    {
        var font = FontFor(size);
        var y = (float)area.Y;
        var lineHeight = font.LineSpacing + lineSpacing;

        text = Sanitise(text, font);

        foreach (var line in Wrap(text, font, area.Width))
        {
            if (y + lineHeight > area.Bottom + 2) break;
            Batch.DrawString(font, line, new Vector2(area.X, y), color);
            y += lineHeight;
        }
    }

    /// <summary>Replaces glyphs the font does not carry, so odd data cannot abort a frame.</summary>
    public string Sanitise(string text, SpriteFont font)
    {
        if (!_glyphs.TryGetValue(font, out var glyphs))
        {
            glyphs = new HashSet<char>(font.Characters);
            _glyphs[font] = glyphs;
        }

        if (text.All(glyphs.Contains)) return text;

        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var c in text) builder.Append(glyphs.Contains(c) ? c : '?');
        return builder.ToString();
    }

    private readonly Dictionary<SpriteFont, HashSet<char>> _glyphs = new();

    /// <summary>Shortens text with an ellipsis so long strings can never bleed out of a card.</summary>
    public string Ellipsis(string text, TextSize size, float maxWidth)
    {
        var font = FontFor(size);
        text = Sanitise(text, font);
        if (font.MeasureString(text).X <= maxWidth) return text;

        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (font.MeasureString(text[..mid] + "…").X <= maxWidth) low = mid;
            else high = mid - 1;
        }

        return text[..low].TrimEnd() + "…";
    }

    public IReadOnlyList<string> Wrap(string text, SpriteFont font, float maxWidth)
    {
        var lines = new List<string>();

        foreach (var paragraph in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                lines.Add(string.Empty);
                continue;
            }

            var builder = new StringBuilder();
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = builder.Length == 0 ? word : builder + " " + word;
                if (font.MeasureString(candidate).X <= maxWidth || builder.Length == 0)
                {
                    builder.Clear();
                    builder.Append(candidate);
                }
                else
                {
                    lines.Add(builder.ToString());
                    builder.Clear();
                    builder.Append(word);
                }
            }

            if (builder.Length > 0) lines.Add(builder.ToString());
        }

        return lines;
    }

    // ---------------------------------------------------------------- controls

    public static Color StyleColor(ButtonStyle style) => style switch
    {
        ButtonStyle.Primary => Theme.Accent,
        ButtonStyle.Danger => Theme.Danger,
        ButtonStyle.Gold => Theme.Gold,
        ButtonStyle.Ghost => new Color(30, 52, 66, 160),
        _ => Theme.PanelLight
    };

    public bool Button(Rectangle rectangle, string text, ButtonStyle style = ButtonStyle.Normal, bool enabled = true)
    {
        var pressed = enabled && Pressed(rectangle);
        var tapped = enabled && Tapped(rectangle);
        var baseColor = StyleColor(style);
        var fill = pressed ? Color.Lerp(baseColor, Color.Black, 0.18f) : baseColor;

        if (!enabled) fill = Color.Lerp(baseColor, Theme.Background, 0.55f);

        NineSlice(new Rectangle(rectangle.X, rectangle.Y + 3, rectangle.Width, rectangle.Height),
            new Color(0, 0, 0, 70));

        NineSlice(rectangle, fill, Theme.ButtonTexture);

        var textColor = style is ButtonStyle.Primary or ButtonStyle.Danger or ButtonStyle.Gold
            ? Theme.ContrastingText(baseColor)
            : Theme.Text;

        if (!enabled) textColor = Theme.TextDim;

        TextIn(text, rectangle, textColor, TextSize.Body, TextAlign.Center);
        return tapped;
    }

    public bool Toggle(Rectangle rectangle, string text, bool value)
    {
        var tapped = Tapped(rectangle);
        var fill = value ? Theme.AccentDark : Theme.PanelLight;
        NineSlice(rectangle, fill, Theme.ButtonTexture);
        Border(rectangle, value ? Theme.Accent : Theme.PanelBorder);

        var indicator = new Rectangle(rectangle.X + 8, rectangle.Y + (rectangle.Height - 20) / 2, 20, 20);
        Fill(indicator, value ? Theme.Accent : Theme.PanelBorder);

        TextIn(text, new Rectangle(rectangle.X + 36, rectangle.Y, rectangle.Width - 44, rectangle.Height),
            Theme.Text, TextSize.Body);
        return tapped;
    }

    public bool Tab(Rectangle rectangle, string text, bool selected, int badge = 0)
    {
        var tapped = Tapped(rectangle);
        var fill = selected ? Theme.Accent : Theme.Panel;
        NineSlice(rectangle, fill);
        if (badge > 0)
        {
            var circle = new Rectangle(rectangle.Right - 30, rectangle.Y + rectangle.Height / 2 - 10, 20, 20);
            Fill(circle, Theme.Danger);
            TextIn(badge.ToString(), circle, Color.White, TextSize.Small, TextAlign.Center);
        }

        TextIn(text, rectangle, selected ? Theme.ContrastingText(Theme.Accent) : Theme.TextMuted, TextSize.Body,
            TextAlign.Center);
        return tapped;
    }

    public void ProgressBar(Rectangle rectangle, float value, Color fill, Color? background = null)
    {
        Fill(rectangle, background ?? new Color(12, 24, 32));
        var width = (int)(Math.Clamp(value, 0f, 1f) * (rectangle.Width - 4));
        if (width > 0) Fill(new Rectangle(rectangle.X + 2, rectangle.Y + 2, width, rectangle.Height - 4), fill);
    }

    public void StatBar(Rectangle rectangle, int value, Color? fill = null)
    {
        var color = fill ?? (value >= 80 ? Theme.Accent : value >= 60 ? Theme.Gold : Theme.Danger);
        ProgressBar(rectangle, value / 99f, color);
    }

    public void Divider(Rectangle area, Color? color = null) =>
        Fill(new Rectangle(area.X, area.Y, area.Width, 1), color ?? Theme.PanelBorder);

    /// <summary>Handles drag/wheel scrolling inside an area and returns the clamped offset.</summary>
    public float HandleScroll(float offset, Rectangle area, float contentHeight)
    {
        var maximum = Math.Max(0, contentHeight - area.Height);
        if (maximum <= 0)
        {
            _dragging = false;
            return 0;
        }

        // Only scroll once the finger has clearly travelled, so a tap never nudges the list.
        if (Input.PointerDown && area.Contains(Input.Pointer.ToPoint()))
        {
            if (!_dragging)
            {
                _dragging = true;
                _dragOriginY = Input.Pointer.Y;
                _dragOffset = offset;
            }

            var travel = Input.Pointer.Y - _dragOriginY;
            if (Math.Abs(travel) > 8) offset = _dragOffset - travel;
        }
        else
        {
            _dragging = false;
        }

        if (Math.Abs(Input.ScrollDelta) > 0.1f && area.Contains(Input.Pointer.ToPoint()))
            offset -= Input.ScrollDelta / 3f;

        return Math.Clamp(offset, 0, maximum);
    }

    private bool _dragging;
    private float _dragOriginY;
    private float _dragOffset;

    public void ScrollIndicator(Rectangle area, float offset, float contentHeight)
    {
        if (contentHeight <= area.Height) return;

        var track = new Rectangle(area.Right - 4, area.Y + 4, 3, area.Height - 8);
        Fill(track, new Color(40, 60, 72));
        var ratio = area.Height / contentHeight;
        var thumbHeight = Math.Max(24, (int)(track.Height * ratio));
        var progress = offset / Math.Max(1, contentHeight - area.Height);
        var y = track.Y + (int)((track.Height - thumbHeight) * progress);
        Fill(new Rectangle(track.X, y, track.Width, thumbHeight), Theme.TextMuted);
    }
}

public enum TextSize
{
    Small,
    Body,
    Subhead,
    Heading,
    Title
}
