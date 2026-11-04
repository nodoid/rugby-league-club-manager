using Microsoft.Xna.Framework;
using RugbyLeague.Core.Model;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public sealed partial class CareerHubScreen
{
    private Formation? _formationDraft;
    private int _draggingShirt;

    private Formation Draft => _formationDraft ??= Club.Formation.Clone();

    private void DrawTactics(Rectangle area)
    {
        var leftWidth = (int)(area.Width * 0.52f);
        var left = new Rectangle(area.X, area.Y, leftWidth, area.Height);
        var right = new Rectangle(left.Right + 16, area.Y, area.Width - leftWidth - 16, area.Height);

        DrawFormationBoard(left);
        DrawTacticalControls(right);
    }

    private void DrawFormationBoard(Rectangle area)
    {
        Ui.Card(area, new Color(13, 26, 35), Theme.PanelBorder);
        Ui.Text("FORMATION", new Vector2(area.X + 16, area.Y + 12), Theme.TextDim, TextSize.Small);
        Ui.Text("Drag a shirt to reshape the attack. Tap a preset to start again.",
            new Vector2(area.X + 16, area.Y + 32), Theme.TextMuted, TextSize.Small);

        var available = new Rectangle(area.X + 16, area.Y + 62, area.Width - 32, area.Height - 96);
        var pitchWidth = Math.Min(available.Width, (int)(available.Height * (100f / 68f)));
        var pitchHeight = (int)(pitchWidth * 68f / 100f);
        if (pitchHeight > available.Height)
        {
            pitchHeight = available.Height;
            pitchWidth = (int)(pitchHeight * 100f / 68f);
        }

        var pitch = new Rectangle(available.X + (available.Width - pitchWidth) / 2,
            available.Y + (available.Height - pitchHeight) / 2, pitchWidth, pitchHeight);

        Ui.Batch.Draw(Theme.PitchTexture, pitch, Color.White * 0.92f);
        Ui.Border(pitch, Theme.PanelBorder, 2);

        // Defensive shape, shown faintly so the manager can see the two lines together.
        foreach (var slot in Draft.Slots)
        {
            var point = PitchPoint(pitch, 50 + slot.DefenceX, 34 + slot.DefenceY);
            Ui.Batch.Draw(Theme.MarkerTexture, new Rectangle((int)point.X - 9, (int)point.Y - 9, 18, 18),
                Theme.Accent * 0.38f);
        }

        var primary = Theme.FromHex(Club.PrimaryColor);
        var secondary = Theme.FromHex(Club.SecondaryColor);

        foreach (var slot in Draft.Slots)
        {
            var point = PitchPoint(pitch, 50 + slot.AttackX, 34 + slot.AttackY);
            var box = new Rectangle((int)point.X - 17, (int)point.Y - 17, 34, 34);

            Ui.Fill(new Rectangle(box.X + 2, box.Y + 3, box.Width, box.Height), new Color(0, 0, 0, 90));
            Ui.Fill(box, primary);
            Ui.Border(box, secondary, 3);
            Ui.TextIn(slot.Shirt.ToString(), box, Theme.ContrastingText(primary), TextSize.Small, TextAlign.Center);
        }

        HandleFormationDrag(pitch);

        var caption = $"{Draft.Name}  ·  attack width {Draft.Profile().AttackWidth:0.00}  ·  line depth {Draft.Profile().DefenceLine:0.0}";
        Ui.Text(caption, new Vector2(area.X + 16, area.Bottom - 28), Theme.TextDim, TextSize.Small);
    }

    private static Vector2 PitchPoint(Rectangle pitch, float metresX, float metresY) =>
        new(pitch.X + Math.Clamp(metresX, 1, 99) / 100f * pitch.Width,
            pitch.Y + Math.Clamp(metresY, 1, 67) / 68f * pitch.Height);

    private void HandleFormationDrag(Rectangle pitch)
    {
        if (Input.PointerPressed && pitch.Contains(Input.Pointer.ToPoint()))
        {
            var nearest = Draft.Slots
                .Select(s => (Slot: s, Distance: Vector2.Distance(PitchPoint(pitch, 50 + s.AttackX, 34 + s.AttackY),
                    Input.Pointer)))
                .OrderBy(pair => pair.Distance)
                .FirstOrDefault();

            if (nearest.Slot is not null && nearest.Distance < 34f) _draggingShirt = nearest.Slot.Shirt;
        }

        if (!Input.PointerDown) _draggingShirt = 0;
        if (_draggingShirt == 0) return;

        var slot = Draft.SlotForShirt(_draggingShirt);
        if (slot is null) return;

        var x = Math.Clamp((Input.Pointer.X - pitch.X) / pitch.Width * 100f - 50f, -30f, 24f);
        var y = Math.Clamp((Input.Pointer.Y - pitch.Y) / pitch.Height * 68f - 34f, -30f, 30f);
        slot.AttackX = MathF.Round(x, 1);
        slot.AttackY = MathF.Round(y, 1);

        // Keep the defensive shape sensible relative to the new attacking position.
        slot.DefenceX = Math.Clamp(slot.AttackX + 14f, -12f, 16f);
        slot.DefenceY = MathF.Round(slot.AttackY * 0.92f, 1);
    }

    private void DrawTacticalControls(Rectangle area)
    {
        var draft = Draft;
        var y = area.Y;

        var presetCard = new Rectangle(area.X, y, area.Width, 132);
        Ui.Card(presetCard, new Color(16, 32, 42), Theme.PanelBorder);
        Ui.Text("PRESETS", new Vector2(presetCard.X + 14, presetCard.Y + 10), Theme.TextDim, TextSize.Small);

        var presets = Formation.Presets();
        var presetWidth = (presetCard.Width - 28 - 8) / 2;
        for (var i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            var column = i % 2;
            var row = i / 2;
            var rectangle = new Rectangle(presetCard.X + 14 + column * (presetWidth + 8),
                presetCard.Y + 32 + row * 32, presetWidth, 28);

            if (Ui.Button(rectangle, preset.Name, ButtonStyle.Ghost, draft.Name != preset.Name))
            {
                _formationDraft = preset.Clone();
                Game.ShowNotice($"{preset.Name} shape loaded.");
            }
        }

        y = presetCard.Bottom + 10;

        var styleCard = new Rectangle(area.X, y, area.Width, 132);
        Ui.Card(styleCard, new Color(16, 32, 42), Theme.PanelBorder);
        Ui.Text("PLAYING STYLE", new Vector2(styleCard.X + 14, styleCard.Y + 10), Theme.TextDim, TextSize.Small);

        var styles = Enum.GetValues<PlayingStyle>();
        var styleWidth = (styleCard.Width - 28 - 8) / 2;
        for (var i = 0; i < styles.Length; i++)
        {
            var style = styles[i];
            var column = i % 2;
            var row = i / 2;
            var rectangle = new Rectangle(styleCard.X + 14 + column * (styleWidth + 8),
                styleCard.Y + 32 + row * 32, styleWidth, 28);

            if (Ui.Toggle(rectangle, Ui.Ellipsis(style.ToString(), TextSize.Small, styleWidth - 42), Club.Tactics.Style == style))
            {
                Club.Tactics.Style = style;
                Club.Style = style;
            }
        }

        y = styleCard.Bottom + 10;

        var sliderCard = new Rectangle(area.X, y, area.Width, area.Bottom - y);
        Ui.Card(sliderCard, new Color(16, 32, 42), Theme.PanelBorder);
        Ui.Text("TACTICAL SLIDERS", new Vector2(sliderCard.X + 14, sliderCard.Y + 10), Theme.TextDim, TextSize.Small);

        var sliderY = sliderCard.Y + 38;
        var kickEarly = Club.Tactics.KickEarly;
        var offloads = Club.Tactics.Offloads;
        var lineSpeed = Club.Tactics.LineSpeed;
        var width = Club.Tactics.Width;

        TacticalSlider(sliderCard, ref sliderY, "Early kicking", ref kickEarly);
        TacticalSlider(sliderCard, ref sliderY, "Offloads", ref offloads);
        TacticalSlider(sliderCard, ref sliderY, "Line speed", ref lineSpeed);
        TacticalSlider(sliderCard, ref sliderY, "Width", ref width);

        Club.Tactics.KickEarly = kickEarly;
        Club.Tactics.Offloads = offloads;
        Club.Tactics.LineSpeed = lineSpeed;
        Club.Tactics.Width = width;

        var valid = draft.IsValid(out var error);
        Ui.Text(Ui.Ellipsis(valid ? "Shape valid: thirteen shirts assigned." : error, TextSize.Small,
                sliderCard.Width - 28),
            new Vector2(sliderCard.X + 14, sliderCard.Bottom - 84), valid ? Theme.TextMuted : Theme.Loss, TextSize.Small);

        if (Ui.Button(new Rectangle(sliderCard.X + 14, sliderCard.Bottom - 56, sliderCard.Width - 28, 42),
                "Save tactics", ButtonStyle.Primary, valid))
        {
            Club.Formation = draft.Clone();
            Game.SaveCareer();
            Game.ShowNotice($"{draft.Name} saved for the next match.");
        }
    }

    private void TacticalSlider(Rectangle card, ref int y, string label, ref int value)
    {
        Ui.Text(label, new Vector2(card.X + 14, y), Theme.TextMuted, TextSize.Small);
        Ui.TextIn(value.ToString(), new Rectangle(card.Right - 64, y - 4, 40, 22), Theme.Text, TextSize.Small,
            TextAlign.Right);

        Ui.ProgressBar(new Rectangle(card.X + 130, y + 5, card.Width - 260, 8), value / 100f,
            value >= 60 ? Theme.Accent : value >= 40 ? Theme.Gold : Theme.TextMuted);

        if (Ui.Button(new Rectangle(card.Right - 116, y - 6, 30, 24), "-", ButtonStyle.Ghost))
            value = Math.Max(0, value - 5);

        if (Ui.Button(new Rectangle(card.Right - 22, y - 6, 30, 24), "+", ButtonStyle.Ghost))
            value = Math.Min(100, value + 5);

        y += 32;
    }
}
