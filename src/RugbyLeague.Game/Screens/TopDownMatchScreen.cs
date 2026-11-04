using Microsoft.Xna.Framework;
using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Model;
using RugbyLeague.Core.Sim;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

/// <summary>
/// Replays the simulated match as a top-down broadcast. The engine has already decided every event;
/// this screen only visualises the frame timeline, which keeps the result independent of frame rate.
/// </summary>
public sealed class TopDownMatchScreen : Screen
{
    private readonly Career _career;
    private readonly MatchResult _result;
    private readonly int _round;
    private float _frameTime;
    private int _frameIndex;
    private float _speed = 4f;
    private bool _paused;
    private bool _submitted;

    public TopDownMatchScreen(RugbyGame game, MatchSetup setup, Career career) : base(game)
    {
        _career = career;
        _round = career.Round;
        _result = MatchEngine.Simulate(setup);
    }

    private MatchFrame? CurrentFrame =>
        _frameIndex >= 0 && _frameIndex < _result.Frames.Count ? _result.Frames[_frameIndex] : null;

    private MatchFrame? NextFrame =>
        _frameIndex + 1 >= 0 && _frameIndex + 1 < _result.Frames.Count ? _result.Frames[_frameIndex + 1] : null;

    public override void Update(GameTime gameTime)
    {
        var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (!_paused && !_submitted) _frameTime += delta * _speed * 3.2f;

        var guard = 0;
        while (CurrentFrame is { } frame && _frameTime >= frame.ReplaySeconds && NextFrame is not null && guard++ < 40)
        {
            _frameTime -= frame.ReplaySeconds;
            _frameIndex++;
        }

        if (NextFrame is null) _frameTime = Math.Min(_frameTime, CurrentFrame?.ReplaySeconds ?? 0);

        if (Input.BackPressed && !_submitted)
        {
            Game.Screens.Pop();
            return;
        }

        if (CurrentFrame is null || (NextFrame is null && _frameTime >= (CurrentFrame?.ReplaySeconds ?? 0)))
        {
            if (!_submitted) Complete();
        }
    }

    private void Complete()
    {
        _submitted = true;
        _career.SubmitPendingMatch(_result);
        Game.SaveCareer();
        Game.Screens.ReplaceAll(new PostMatchScreen(Game, _result, _round));
    }

    public override void Draw()
    {
        Ui.Fill(View.Bounds, new Color(7, 16, 22));

        var area = Page;
        var hud = new Rectangle(area.X, area.Y, area.Width, 76);
        Ui.Card(hud, new Color(13, 27, 36), Theme.PanelBorder);

        var home = _career.TryTeam(_result.HomeTeamId);
        var away = _career.TryTeam(_result.AwayTeamId);

        var (homeScore, awayScore) = ScoresAt(CurrentFrame?.Minute ?? 0);

        Ui.TextIn(Ui.Ellipsis(home?.Name ?? _result.HomeTeamName, TextSize.Small, 200),
            new Rectangle(hud.Center.X - 300, hud.Y + 32, 200, 24), Theme.Text, TextSize.Small, TextAlign.Right);
        Ui.TextIn($"{homeScore}", new Rectangle(hud.Center.X - 92, hud.Y + 28, 44, 32), Theme.Gold,
            TextSize.Subhead, TextAlign.Right);
        Ui.TextIn("–", new Rectangle(hud.Center.X - 44, hud.Y + 30, 30, 26), Theme.TextMuted, TextSize.Small,
            TextAlign.Center);
        Ui.TextIn($"{awayScore}", new Rectangle(hud.Center.X + 6, hud.Y + 28, 44, 32), Theme.Gold, TextSize.Subhead,
            TextAlign.Left);
        Ui.TextIn(Ui.Ellipsis(away?.Name ?? _result.AwayTeamName, TextSize.Small, 200),
            new Rectangle(hud.Center.X + 58, hud.Y + 32, 200, 24), Theme.Text, TextSize.Small, TextAlign.Left);

        Ui.TextIn($"{(_paused ? "PAUSED" : "LIVE")}  ·  {CurrentFrame?.Minute ?? 0:0}'",
            new Rectangle(hud.Center.X - 110, hud.Y + 8, 220, 24),
            _paused ? Theme.Gold : Theme.Accent, TextSize.Small, TextAlign.Center);
        Ui.TextIn(Ui.Ellipsis($"{_result.Competition}  ·  {_result.RoundName}", TextSize.Small, 360),
            new Rectangle(hud.Center.X - 190, hud.Y + 54, 380, 20), Theme.TextDim, TextSize.Small, TextAlign.Center);

        var tackle = CurrentFrame?.Tackle ?? 0;
        Ui.TextIn($"Tackle {Math.Clamp(tackle, 1, 6)} / 6",
            new Rectangle(hud.Right - 440, hud.Y + 12, 160, 24), Theme.TextMuted, TextSize.Small, TextAlign.Right);

        if (Ui.Button(new Rectangle(hud.Right - 268, hud.Y + 16, 76, 40), _paused ? "Play" : "Pause",
                ButtonStyle.Ghost))
            _paused = !_paused;

        foreach (var speed in new[] { 1f, 4f, 12f })
        {
            var x = hud.Right - 186 + (speed == 1 ? 0 : speed == 4 ? 54 : 108);
            if (Ui.Button(new Rectangle((int)x, hud.Y + 16, 48, 40), $"{speed:0}x",
                    _speed == speed ? ButtonStyle.Primary : ButtonStyle.Ghost))
                _speed = speed;
        }

        var pitchArea = new Rectangle(area.X, hud.Bottom + 10, area.Width, area.Height - hud.Height - 92);
        var pitchWidth = Math.Min(pitchArea.Width, (int)(pitchArea.Height * 100f / 68f));
        var pitchHeight = (int)(pitchWidth * 68f / 100f);
        if (pitchHeight > pitchArea.Height)
        {
            pitchHeight = pitchArea.Height;
            pitchWidth = (int)(pitchHeight * 100f / 68f);
        }

        var pitch = new Rectangle(pitchArea.X + (pitchArea.Width - pitchWidth) / 2,
            pitchArea.Y + (pitchArea.Height - pitchHeight) / 2, pitchWidth, pitchHeight);

        Ui.Batch.Draw(Theme.PitchTexture, pitch, Color.White);
        Ui.Border(pitch, new Color(8, 20, 16), 3);

        DrawPlayers(pitch);

        var ticker = new Rectangle(area.X, area.Bottom - 72, area.Width, 72);
        Ui.Card(ticker, new Color(13, 27, 36), Theme.PanelBorder);
        DrawCommentary(ticker);

        if (NextFrame is null)
        {
            Ui.Fill(View.Bounds, new Color(4, 10, 14, 200));
            var panel = new Rectangle((int)(View.Width / 2 - 260), (int)(View.Height / 2 - 110), 520, 220);
            Ui.Card(panel, new Color(18, 34, 44), Theme.Accent);
            Ui.TextIn("FULL TIME", new Rectangle(panel.X, panel.Y + 24, panel.Width, 30), Theme.Gold, TextSize.Subhead,
                TextAlign.Center);
            Ui.TextIn($"{_result.HomeTeamName} {_result.HomeScore} - {_result.AwayScore} {_result.AwayTeamName}",
                new Rectangle(panel.X + 10, panel.Y + 72, panel.Width - 20, 40), Theme.Text, TextSize.Body,
                TextAlign.Center);
            Ui.TextIn(_result.GoldenPoint ? "Decided in golden point" : _result.RoundName,
                new Rectangle(panel.X, panel.Y + 112, panel.Width, 24), Theme.TextMuted, TextSize.Small,
                TextAlign.Center);

            if (Ui.Button(new Rectangle(panel.X + 60, panel.Y + 150, 400, 48), "Continue", ButtonStyle.Primary))
                Complete();
        }
    }

    private (int Home, int Away) ScoresAt(float minute)
    {
        var home = 0;
        var away = 0;
        foreach (var matchEvent in _result.Events)
        {
            if (matchEvent.Minute > minute) break;
            if (matchEvent.Kind is MatchEventKind.Try or MatchEventKind.Conversion or MatchEventKind.PenaltyGoal
                or MatchEventKind.DropGoal)
            {
                home = matchEvent.HomeScore;
                away = matchEvent.AwayScore;
            }
        }

        return (home, away);
    }

    private void DrawPlayers(Rectangle pitch)
    {
        var frame = CurrentFrame;
        var next = NextFrame;
        if (frame is null) return;

        var t = frame.ReplaySeconds <= 0.01f ? 0f : Math.Clamp(_frameTime / frame.ReplaySeconds, 0f, 1f);

        foreach (var player in frame.Players)
        {
            var target = next?.Players.FirstOrDefault(p => p.TeamId == player.TeamId && p.Shirt == player.Shirt);
            var x = target is null ? player.X : MathHelper.Lerp(player.X, target.X, t);
            var y = target is null ? player.Y : MathHelper.Lerp(player.Y, target.Y, t);

            var position = new Vector2(pitch.X + x / 100f * pitch.Width, pitch.Y + y / 68f * pitch.Height);
            var team = _career.TryTeam(player.TeamId);
            var primary = Theme.FromHex(team?.PrimaryColor ?? "#FFFFFF");

            var size = Math.Max(14, pitch.Width / 34);
            var box = new Rectangle((int)position.X - size / 2, (int)position.Y - size / 2, size, size);
            Ui.Batch.Draw(Theme.MarkerTexture, box, primary);
            Ui.TextIn(player.Shirt.ToString(), box, Theme.ContrastingText(primary),
                size > 22 ? TextSize.Small : TextSize.Small, TextAlign.Center);
        }

        var ballX = frame.BallX;
        var ballY = frame.BallY;
        if (next is not null)
        {
            ballX = MathHelper.Lerp(frame.BallX, next.BallX, t);
            ballY = MathHelper.Lerp(frame.BallY, next.BallY, t);
        }

        var ballSize = Math.Max(16, pitch.Width / 40);
        var ballRect = new Rectangle((int)(pitch.X + ballX / 100f * pitch.Width) - ballSize / 2,
            (int)(pitch.Y + ballY / 68f * pitch.Height) - ballSize / 2, ballSize, ballSize);
        Ui.Batch.Draw(Theme.BallTexture, ballRect, Color.White);
    }

    private void DrawCommentary(Rectangle ticker)
    {
        var minute = CurrentFrame?.Minute ?? 0;
        var events = _result.Commentary.Where(e => e.Minute <= minute).TakeLast(2).ToList();

        if (events.Count == 0)
        {
            Ui.TextIn("Kick-off", ticker, Theme.TextMuted, TextSize.Small, TextAlign.Left);
            return;
        }

        var y = ticker.Y + 12;
        foreach (var matchEvent in events)
        {
            var colour = matchEvent.Kind switch
            {
                MatchEventKind.Try => Theme.Gold,
                MatchEventKind.Conversion or MatchEventKind.PenaltyGoal => Theme.Accent,
                MatchEventKind.Injury or MatchEventKind.HeadAssessment => Theme.Loss,
                MatchEventKind.SinBin => Theme.Gold,
                _ => Theme.TextMuted
            };

            Ui.Text($"{matchEvent.Minute}'  {matchEvent.Text}",
                new Vector2(ticker.X + 18, y), colour, TextSize.Small);
            y += 26;
        }
    }
}
