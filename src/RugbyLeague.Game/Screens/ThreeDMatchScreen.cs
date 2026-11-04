using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Model;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

/// <summary>
/// Arcade 3D rugby league. The manager controls the ball carrier in attack and the nearest defender
/// in defence, while the rest of the seventeen plays to the chosen formation and tactics.
/// </summary>
public sealed partial class ThreeDMatchScreen : Screen
{
    private readonly Career _career;
    private readonly MatchSetup _setup;
    private bool _playerIsHome;

    private BasicEffect? _colorEffect;
    private BasicEffect? _groundEffect;
    private VertexPositionColor[] _staticMesh = Array.Empty<VertexPositionColor>();
    private VertexPositionColor[] _postMesh = Array.Empty<VertexPositionColor>();
    private VertexPositionTexture[] _groundMesh = Array.Empty<VertexPositionTexture>();
    private float _layoutWidth;

    private Vector3 _cameraPosition = new(-14, 13, 34);
    private Vector3 _cameraLook = new(40, 0, 34);

    public ThreeDMatchScreen(RugbyGame game, MatchSetup setup, Career career) : base(game)
    {
        _setup = setup;
        _career = career;
    }

    public override void Update(GameTime gameTime)
    {
        if (Input.BackPressed && !_finished)
        {
            Game.Screens.Pop();
            return;
        }

        if (!_initialised) Initialise();

        if (Math.Abs(_layoutWidth - View.Width) > 1) LayoutControls();

        Simulate((float)gameTime.ElapsedGameTime.TotalSeconds);

        if (_finished && !_submitted && (Input.PointerPressed || Input.IsNewKeyPress(Microsoft.Xna.Framework.Input.Keys.Enter)))
            Submit();
    }

    private void Submit()
    {
        _submitted = true;
        var round = _career.Round;
        _career.SubmitPendingMatch(_result!);
        Game.SaveCareer();
        Game.Screens.ReplaceAll(new PostMatchScreen(Game, _result!, round));
    }

    // ---------------------------------------------------------------- rendering

    public override void DrawWorld(GraphicsDevice device)
    {
        if (!_initialised) return;

        EnsureGraphics(device);

        var aspect = device.Viewport.AspectRatio;
        var view = Matrix.CreateLookAt(_cameraPosition, _cameraLook,
            Vector3.Up);
        var projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(58f), aspect, 0.2f, 500f);

        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullNone;
        device.BlendState = BlendState.Opaque;
        device.SamplerStates[0] = SamplerState.LinearWrap;

        if (_groundEffect is not null)
        {
            _groundEffect.View = view;
            _groundEffect.Projection = projection;
            foreach (var pass in _groundEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, _groundMesh, 0, _groundMesh.Length / 3);
            }
        }

        if (_colorEffect is null) return;

        _colorEffect.View = view;
        _colorEffect.Projection = projection;

        foreach (var pass in _colorEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, _staticMesh, 0, _staticMesh.Length / 3);
            device.DrawUserPrimitives(PrimitiveType.TriangleList, _postMesh, 0, _postMesh.Length / 3);

            var dynamicMesh = BuildActorMesh();
            if (dynamicMesh.Length > 0)
                device.DrawUserPrimitives(PrimitiveType.TriangleList, dynamicMesh, 0, dynamicMesh.Length / 3);
        }
    }

    private void EnsureGraphics(GraphicsDevice device)
    {
        if (_colorEffect is not null) return;

        _colorEffect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            TextureEnabled = false
        };

        _groundEffect = new BasicEffect(device)
        {
            TextureEnabled = true,
            Texture = Theme.GrassTexture,
            VertexColorEnabled = false,
            LightingEnabled = false
        };

        _groundMesh = BuildGround();
        _staticMesh = BuildStaticMesh();
        _postMesh = BuildPosts();
    }

    private static VertexPositionTexture[] BuildGround()
    {
        const float x0 = 0, x1 = PitchLength, z0 = 0, z1 = PitchWidth;
        const float u0 = 0, u1 = 12, v0 = 0, v1 = 8;
        var y = 0f;

        return new[]
        {
            new VertexPositionTexture(new Vector3(x0, y, z0), new Vector2(u0, v0)),
            new VertexPositionTexture(new Vector3(x1, y, z0), new Vector2(u1, v0)),
            new VertexPositionTexture(new Vector3(x1, y, z1), new Vector2(u1, v1)),
            new VertexPositionTexture(new Vector3(x0, y, z0), new Vector2(u0, v0)),
            new VertexPositionTexture(new Vector3(x1, y, z1), new Vector2(u1, v1)),
            new VertexPositionTexture(new Vector3(x0, y, z1), new Vector2(u0, v1))
        };
    }

    private VertexPositionColor[] BuildStaticMesh()
    {
        var mesh = new MeshBuilder();

        // Dark surrounds and advertising boards give the pitch a stadium feel.
        var surround = new Color(12, 20, 24);
        mesh.AddBox(new Vector3(50, -0.6f, -3), new Vector3(112, 1.2f, 6), surround);
        mesh.AddBox(new Vector3(50, -0.6f, 71), new Vector3(112, 1.2f, 6), surround);

        var homeColor = Theme.FromHex(_setup.Home.PrimaryColor);
        var awayColor = Theme.FromHex(_setup.Away.PrimaryColor);

        for (var i = 0; i < 10; i++)
        {
            var x = 2 + i * 10.6f;
            mesh.AddBox(new Vector3(x, 0.9f, -0.9f), new Vector3(9.4f, 1.6f, 0.2f),
                i % 2 == 0 ? homeColor * 0.8f : awayColor * 0.8f);
            mesh.AddBox(new Vector3(x, 0.9f, 68.9f), new Vector3(9.4f, 1.6f, 0.2f),
                i % 2 == 0 ? awayColor * 0.8f : homeColor * 0.8f);
        }

        // Stands.
        var stand = new Color(24, 34, 40);
        mesh.AddBox(new Vector3(50, 2.4f, -7.5f), new Vector3(112, 6f, 6), stand);
        mesh.AddBox(new Vector3(50, 2.4f, 75.5f), new Vector3(112, 6f, 6), stand);

        var line = new Color(242, 246, 242);

        // Perimeter, halfway and ten-metre lines.
        AddLine(mesh, new Vector3(0, 0.06f, 34), 0.14f, PitchWidth, line);
        AddLine(mesh, new Vector3(PitchLength, 0.06f, 34), 0.14f, PitchWidth, line);
        AddLine(mesh, new Vector3(50, 0.06f, 34), 0.14f, PitchWidth, line);
        AddLine(mesh, new Vector3(10, 0.06f, 34), 0.2f, PitchWidth, line, 1.15f);
        AddLine(mesh, new Vector3(90, 0.06f, 34), 0.2f, PitchWidth, line, 1.15f);

        for (var i = 1; i <= 8; i++)
        {
            var x = i * 10f;
            if (i is 1 or 5 or 9) continue;
            AddLine(mesh, new Vector3(x, 0.05f, 34), 0.1f, PitchWidth, line * 0.65f);
        }

        // In-goal areas.
        mesh.AddBox(new Vector3(5, 0.01f, 34), new Vector3(10, 0.01f, PitchWidth), new Color(24, 84, 50));
        mesh.AddBox(new Vector3(95, 0.01f, 34), new Vector3(10, 0.01f, PitchWidth), new Color(24, 84, 50));

        return mesh.ToArray();
    }

    private static void AddLine(MeshBuilder mesh, Vector3 centre, float width, float length, Color color,
        float height = 1f)
    {
        mesh.AddBox(new Vector3(centre.X, centre.Y, centre.Z), new Vector3(width, 0.02f * height, length), color);
    }

    private static VertexPositionColor[] BuildPosts()
    {
        var mesh = new MeshBuilder();
        var white = new Color(248, 248, 244);

        foreach (var x in new[] { 10f, 90f })
        {
            foreach (var z in new[] { 34f - 2.9f, 34f + 2.9f })
            {
                mesh.AddCylinder(new Vector3(x, 0, z), 0.1f, 5.6f, white, 8);
            }

            mesh.AddBox(new Vector3(x, 3.1f, 34), new Vector3(0.1f, 0.12f, 5.9f), white);

            // Padding at the base of each post.
            foreach (var z in new[] { 34f - 2.9f, 34f + 2.9f })
                mesh.AddCylinder(new Vector3(x, 0, z), 0.18f, 1.1f, new Color(198, 58, 52), 8);
        }

        return mesh.ToArray();
    }

    private VertexPositionColor[] BuildActorMesh()
    {
        var mesh = new MeshBuilder();

        foreach (var actor in _actors)
        {
            var primary = Theme.FromHex(Sheet(actor.IsHome).PrimaryColor);
            var secondary = Theme.FromHex(Sheet(actor.IsHome).SecondaryColor);

            if (actor.IsControlled)
                mesh.AddRing(new Vector3(actor.Position.X, 0.09f, actor.Position.Z), 0.62f, 0.85f, Theme.Gold, 20);
            else if (actor == _carrier)
                mesh.AddRing(new Vector3(actor.Position.X, 0.09f, actor.Position.Z), 0.55f, 0.72f, Color.White, 16);

            mesh.AddDisc(new Vector3(actor.Position.X, 0.07f, actor.Position.Z), 0.55f, new Color(6, 18, 12));

            mesh.AddCylinder(new Vector3(actor.Position.X, 0.15f, actor.Position.Z), 0.34f, 0.9f,
                Color.Lerp(secondary, Color.Black, 0.15f), 8, false);
            mesh.AddCylinder(new Vector3(actor.Position.X, 0.85f, actor.Position.Z), 0.42f, 0.75f,
                primary, 8);
            mesh.AddSphere(new Vector3(actor.Position.X, 1.85f, actor.Position.Z), 0.24f,
                new Color(222, 182, 150), 8, 5);
        }

        // A rugby ball is an oval: about 28 cm long and 18 cm wide, so it is drawn as a prolate
        // spheroid rather than a football.
        mesh.AddEllipsoid(new Vector3(_ball.X, MathF.Max(0.35f, _ball.Y), _ball.Z), 0.62f, 0.34f, 0.36f,
            new Color(245, 238, 214));

        return mesh.ToArray();
    }

    // ---------------------------------------------------------------- HUD

    public override void Draw() => DrawHud();

    private void DrawHud()
    {
        var width = View.Width;
        var height = View.Height;

        var boardWidth = Math.Min(620, (int)width - 240);
        var board = new Rectangle((int)(width / 2 - boardWidth / 2f), 20, boardWidth, 78);
        Ui.Card(board, new Color(10, 22, 30) * 0.94f, Theme.PanelBorder);

        var homeName = _setup.Home.Nickname;
        var awayName = _setup.Away.Nickname;

        Ui.Text(homeName, new Vector2(board.X + 20, board.Y + 12), Theme.Text, TextSize.Small);
        Ui.TextIn($"{_homeScore}", new Rectangle(board.Center.X - 150, board.Y + 8, 70, 40), Theme.Gold,
            TextSize.Subhead, TextAlign.Right);
        Ui.TextIn($"{_awayScore}", new Rectangle(board.Center.X + 80, board.Y + 8, 70, 40), Theme.Gold,
            TextSize.Subhead, TextAlign.Left);
        Ui.TextIn("-", new Rectangle(board.Center.X - 74, board.Y + 12, 40, 30), Theme.TextDim, TextSize.Body,
            TextAlign.Center);
        Ui.TextIn($"{Math.Min(80, _clock):0}'  ·  {(_goldenPoint ? "ET" : $"Half {_half}")}",
            new Rectangle(board.Center.X - 80, board.Y + 44, 160, 20), Theme.TextMuted, TextSize.Small,
            TextAlign.Center);

        Ui.TextIn(awayName, new Rectangle(board.Right - 180, board.Y + 12, 160, 24), Theme.Text, TextSize.Small,
            TextAlign.Right);

        var possessionDot = new Rectangle(board.X + 6, board.Y + 26, 6, 30);
        Ui.Fill(possessionDot, _homePossession ? Theme.Gold : Theme.AwayBar);

        var tackleX = board.Center.X - 60;
        for (var i = 0; i < 6; i++)
        {
            var dot = new Rectangle(tackleX + i * 20, board.Bottom - 11, 12, 5);
            Ui.Fill(dot, i < _tackle ? Theme.Gold : new Color(48, 66, 74));
        }

        DrawWind();
        DrawDiscipline();

        if (_phase == MatchPhase.Conversion)
        {
            DrawConversion(width, height);
        }
        else
        {
            // Joystick.
            var stickBase = new Rectangle(_stickArea.Center.X - 96, _stickArea.Center.Y - 96, 192, 192);
            Ui.Batch.Draw(Theme.MarkerTexture, stickBase, new Color(30, 46, 54) * 0.55f);
            Ui.Border(stickBase, new Color(120, 150, 150) * 0.5f, 2);

            var knob = new Vector2(_stickCentre.X + _stickValue.X * _stickRadius,
                _stickCentre.Y + _stickValue.Y * _stickRadius);
            var knobRect = new Rectangle((int)knob.X - 34, (int)knob.Y - 34, 68, 68);
            Ui.Batch.Draw(Theme.MarkerTexture, knobRect, new Color(212, 228, 220) * 0.85f);

            DrawControlButton(_passButton, "PASS", Theme.Accent, Input.AnyPointerIn(_passButton));
            DrawControlButton(_kickButton, "KICK", Theme.Gold, Input.AnyPointerIn(_kickButton));
            DrawControlButton(_tackleButton, "TACKLE", Theme.Danger,
                Input.AnyPointerIn(_tackleButton) || _tacklePressed);
            DrawControlButton(_sprintButton, "RUN", new Color(70, 96, 108), _sprintHeld > 0);
        }

        if (_messageTime > 0 && !string.IsNullOrEmpty(_message))
        {
            var font = Theme.Subhead;
            var messageWidth = Math.Min((int)width - 80, (int)font.MeasureString(_message).X + 64);
            var banner = new Rectangle((int)(width / 2 - messageWidth / 2f), (int)(height * 0.30f), messageWidth, 52);
            Ui.NineSlice(banner, new Color(8, 20, 26) * 0.9f);
            Ui.Border(banner, Theme.Gold, 2);
            Ui.TextIn(_message, banner, Theme.Text, TextSize.Subhead, TextAlign.Center);
        }

        Ui.TextIn(_phase == MatchPhase.Conversion
                ? "Goal kicking  ·  power and wind decide it"
                : "Drag the stick to run  ·  PASS  ·  KICK  ·  TACKLE  ·  RUN to sprint",
            new Rectangle((int)(width / 2 - 280), (int)height - 34, 560, 24), Theme.TextDim * 0.9f,
            TextSize.Small, TextAlign.Center);

        if (_finished) DrawFullTime();
    }

    /// <summary>Wind dial in the top-right corner; it drives the conversions and the kicking game.</summary>
    private void DrawWind()
    {
        var card = new Rectangle((int)View.Width - 288, 20, 268, 78);
        Ui.Card(card, new Color(10, 22, 30) * 0.94f, Theme.PanelBorder);

        Ui.Text("WIND", new Vector2(card.X + 14, card.Y + 10), Theme.TextDim, TextSize.Small);
        Ui.Text(_setup.Weather.WindLabel, new Vector2(card.X + 14, card.Y + 34), Theme.Text, TextSize.Body);

        var centre = new Vector2(card.Right - 44, card.Y + 46);
        var angle = MathHelper.ToRadians(_setup.Weather.WindBearing);
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

        if (_setup.Weather.WindSpeed >= 3)
        {
            Ui.Batch.Draw(Theme.Pixel, centre, null, Theme.Gold, angle, new Vector2(0f, 0.5f),
                new Vector2(30f, 4f), SpriteEffects.None, 0f);

            var tip = centre + direction * 30f;
            foreach (var spread in new[] { 2.5f, -2.5f })
                Ui.Batch.Draw(Theme.Pixel, tip, null, Theme.Gold, angle + MathF.PI + spread,
                    new Vector2(0f, 0.5f), new Vector2(11f, 4f), SpriteEffects.None, 0f);
        }
        else
        {
            Ui.Fill(new Rectangle((int)centre.X - 12, (int)centre.Y - 2, 24, 4), Theme.TextDim);
        }
    }

    /// <summary>Sin bin and head injury assessment countdowns, so both are visible on the field.</summary>
    private string Nickname(bool home) => home ? _setup.Home.Nickname : _setup.Away.Nickname;

    private void DrawDiscipline()
    {
        var rows = new List<(string Label, string Time, Color Colour)>();

        foreach (var (actor, until) in _sinBins)
        {
            rows.Add(($"{Nickname(actor.IsHome)} · {actor.Player.ShortName}",
                $"BIN {MathF.Ceiling(until - _clock):0}'", Theme.Gold));
        }

        foreach (var assessment in _headAssessments)
        {
            rows.Add(($"{Nickname(assessment.IsHome)} · {assessment.Player.Player.ShortName}",
                $"HIA {MathF.Ceiling(assessment.ReturnAt - _clock):0}'", Theme.Loss));
        }

        if (rows.Count == 0) return;

        var card = new Rectangle((int)View.Width - 288, 106, 268, 30 + rows.Count * 22);
        Ui.Card(card, new Color(10, 22, 30) * 0.94f, Theme.PanelBorder);
        Ui.Text("DISCIPLINE", new Vector2(card.X + 14, card.Y + 8), Theme.TextDim, TextSize.Small);

        var y = card.Y + 28;
        foreach (var (label, time, colour) in rows)
        {
            Ui.Text(label, new Vector2(card.X + 14, y), Theme.Text, TextSize.Small);
            Ui.TextIn(time, new Rectangle(card.Right - 90, y, 76, 20), colour, TextSize.Small, TextAlign.Right);
            y += 22;
        }
    }

    /// <summary>The conversion HUD: placement instructions, power meter and the strike button.</summary>
    private void DrawConversion(float width, float height)
    {
        // Kept to the left so the ball, the kicker and the posts stay in view.
        var panel = new Rectangle(24, (int)height - 236, 470, 190);
        Ui.Card(panel, new Color(10, 22, 30) * 0.95f, Theme.PanelBorder);

        var kicker = _kicker?.Player.Name ?? "Kicker";
        Ui.TextIn($"CONVERSION  ·  {TeamName(_conversionHome)}",
            new Rectangle(panel.X, panel.Y + 10, panel.Width, 24), Theme.Gold, TextSize.Small, TextAlign.Center);
        Ui.TextIn($"{kicker}  ·  {ConversionDistance:0} m  ·  wind {_setup.Weather.WindLabel}",
            new Rectangle(panel.X, panel.Y + 34, panel.Width, 22), Theme.TextMuted, TextSize.Small, TextAlign.Center);

        var meter = new Rectangle(panel.X + 40, panel.Y + 72, panel.Width - 80, 34);
        Ui.Fill(meter, new Color(7, 16, 21));

        var targetStart = (int)((_conversionRequiredPower - 0.055f) * meter.Width);
        var targetWidth = (int)(0.11f * meter.Width);
        Ui.Fill(new Rectangle(meter.X + targetStart, meter.Y, targetWidth, meter.Height), Theme.Accent * 0.35f);
        Ui.Fill(new Rectangle(meter.X, meter.Y, (int)(Math.Clamp(_power, 0f, 1f) * meter.Width), meter.Height),
            Theme.Gold * 0.9f);
        Ui.Border(meter, Theme.PanelBorder, 2);

        Ui.TextIn("POWER", new Rectangle(meter.X, meter.Y - 20, 100, 18), Theme.TextDim, TextSize.Small);
        Ui.TextIn($"{_power * 100f:0}%", new Rectangle(meter.Right - 100, meter.Y - 20, 100, 18), Theme.TextMuted,
            TextSize.Small, TextAlign.Right);

        var hint = _conversionStage switch
        {
            ConversionStage.Place => "Drag left or right to place the ball, then tap KICK",
            ConversionStage.Power => "Tap KICK as the needle crosses the green band",
            ConversionStage.Flight => "The kick is away…",
            _ => _conversionResult == "GOAL" ? "It's over!" : "Wide of the posts"
        };

        Ui.TextIn(hint, new Rectangle(panel.X + 12, panel.Y + 122, panel.Width - 24, 24), Theme.Text, TextSize.Small,
            TextAlign.Center);

        var action = _conversionStage == ConversionStage.Power ? "STRIKE" : "KICK";
        DrawControlButton(_kickButton, action, Theme.Gold, Input.AnyPointerIn(_kickButton));
    }

    private void DrawControlButton(Rectangle rectangle, string label, Color colour, bool pressed)
    {
        var tint = pressed ? Color.Lerp(colour, Color.White, 0.3f) : colour * 0.9f;
        Ui.Batch.Draw(Theme.MarkerTexture, rectangle, tint);
        Ui.TextIn(label, rectangle, Theme.Text, TextSize.Small, TextAlign.Center);
    }

    private void DrawFullTime()
    {
        Ui.Fill(View.Bounds, new Color(4, 10, 14, 190));

        var panel = new Rectangle((int)(View.Width / 2 - 300), (int)(View.Height / 2 - 140), 600, 280);
        Ui.Card(panel, new Color(16, 32, 42), Theme.Accent);

        Ui.TextIn("FULL TIME", new Rectangle(panel.X, panel.Y + 22, panel.Width, 32), Theme.Gold, TextSize.Subhead,
            TextAlign.Center);
        Ui.TextIn($"{_setup.Home.TeamName}  {_homeScore} - {_awayScore}  {_setup.Away.TeamName}",
            new Rectangle(panel.X + 16, panel.Y + 68, panel.Width - 32, 34), Theme.Text, TextSize.Body,
            TextAlign.Center);
        Ui.TextIn(_goldenPoint ? "Golden point" : _setup.CompetitionName,
            new Rectangle(panel.X, panel.Y + 110, panel.Width, 24), Theme.TextMuted, TextSize.Small, TextAlign.Center);

        var tries = _playerStats.Values.Where(s => s.Tries > 0).OrderByDescending(s => s.Tries).Take(3).ToList();
        var y = panel.Y + 142;
        foreach (var scorer in tries)
        {
            Ui.TextIn($"{scorer.Name}  ·  {scorer.Tries} try", new Rectangle(panel.X, y, panel.Width, 22),
                Theme.TextMuted, TextSize.Small, TextAlign.Center);
            y += 20;
        }

        if (Ui.Button(new Rectangle(panel.X + 90, panel.Bottom - 70, panel.Width - 180, 50), "Continue",
                ButtonStyle.Primary))
            Submit();
    }
}
