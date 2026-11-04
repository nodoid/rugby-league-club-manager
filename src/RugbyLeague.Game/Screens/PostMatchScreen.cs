using Microsoft.Xna.Framework;
using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Model;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public sealed class PostMatchScreen : Screen
{
    private readonly MatchResult _result;
    private readonly int _round;
    private readonly Career? _career;

    public PostMatchScreen(RugbyGame game, MatchResult result, int round) : base(game)
    {
        _result = result;
        _round = round;
        _career = game.Career;
    }

    public override void Update(GameTime gameTime)
    {
        if (Input.BackPressed) Continue();
    }

    public override void Draw()
    {
        DrawCover(Theme.PitchTexture, new Color(20, 32, 32));
        Ui.Fill(View.Bounds, new Color(6, 16, 22, 234));

        var area = Page;
        var header = new Rectangle(area.X, area.Y, area.Width, 60);
        Ui.Text("FULL TIME", new Vector2(header.X, header.Y + 4), Theme.Gold, TextSize.Heading);
        Ui.Text($"{_result.Competition}  ·  {_result.RoundName}  ·  {_result.Venue}  ·  {_result.Weather}",
            new Vector2(header.X + 240, header.Y + 16), Theme.TextMuted, TextSize.Small);

        if (Ui.Button(new Rectangle(header.Right - 190, header.Y + 4, 190, 48), "Continue", ButtonStyle.Primary))
        {
            Continue();
            return;
        }

        var card = new Rectangle(area.X, area.Y + 72, area.Width, area.Height - 72);
        Ui.Card(card, new Color(14, 28, 38), Theme.PanelBorder);

        var home = _career?.TryTeam(_result.HomeTeamId);
        var away = _career?.TryTeam(_result.AwayTeamId);
        var playerIsHome = _career is not null && _result.HomeTeamId == _career.PlayerTeamId;
        var playerWon = _career is not null &&
                        (_result.WinnerId == _career.PlayerTeamId);
        var playerDrew = _career is not null && _result.IsDraw && !_result.GoldenPoint;

        var scoreCard = new Rectangle(card.X + 20, card.Y + 18, card.Width - 40, 116);
        Ui.Fill(scoreCard, new Color(10, 22, 30));
        Ui.Fill(new Rectangle(scoreCard.X, scoreCard.Y, 6, scoreCard.Height),
            playerWon ? Theme.Win : playerDrew ? Theme.Draw : _career is null ? Theme.Accent : Theme.Loss);

        DrawBadge(new Rectangle(scoreCard.X + 28, scoreCard.Y + 22, 72, 72), home, _result.HomeTeamName);
        Ui.Text(_result.HomeTeamName, new Vector2(scoreCard.X + 116, scoreCard.Y + 24), Theme.Text, TextSize.Subhead);
        Ui.Text($"{(playerIsHome ? "Your club" : "Opponent")}  ·  {_result.HomeTries} tries",
            new Vector2(scoreCard.X + 116, scoreCard.Y + 60), Theme.TextMuted, TextSize.Small);

        Ui.TextIn($"{_result.HomeScore}", new Rectangle(scoreCard.Center.X - 150, scoreCard.Y + 26, 120, 64),
            Theme.Gold, TextSize.Title, TextAlign.Right);
        Ui.TextIn($"{_result.AwayScore}", new Rectangle(scoreCard.Center.X + 30, scoreCard.Y + 26, 120, 64),
            Theme.Gold, TextSize.Title, TextAlign.Left);
        Ui.TextIn("-", new Rectangle(scoreCard.Center.X - 26, scoreCard.Y + 36, 52, 40), Theme.TextDim,
            TextSize.Subhead, TextAlign.Center);

        DrawBadge(new Rectangle(scoreCard.Right - 100, scoreCard.Y + 22, 72, 72), away, _result.AwayTeamName);
        Ui.TextIn(_result.AwayTeamName, new Rectangle(scoreCard.Right - 470, scoreCard.Y + 24, 360, 30),
            Theme.Text, TextSize.Subhead, TextAlign.Right);
        Ui.TextIn($"{(playerIsHome ? "Opponent" : "Your club")}  ·  {_result.AwayTries} tries",
            new Rectangle(scoreCard.Right - 470, scoreCard.Y + 60, 360, 24), Theme.TextMuted, TextSize.Small,
            TextAlign.Right);

        var left = new Rectangle(card.X + 20, scoreCard.Bottom + 18, (int)(card.Width * 0.46f), card.Height - 170);
        var right = new Rectangle(left.Right + 16, left.Y, card.Width - left.Width - 56, left.Height);

        DrawStats(left);
        DrawScorersAndInjuries(right);

        var verdict = _result.GoldenPoint
            ? "Golden point drama"
            : playerDrew
                ? "Honours even"
                : playerWon
                    ? "Victory"
                    : "Defeat";

        if (_career is not null)
            Ui.Text($"{verdict}  ·  {_career.PlayerTeam.Name}  ·  {_career.PlayerLeague.Name}",
                new Vector2(card.X + 20, card.Bottom - 34), Theme.TextMuted, TextSize.Small);
    }

    private void Continue()
    {
        Game.SaveCareer();
        Game.Screens.ReplaceAll(new CareerHubScreen(Game));
    }

    private void DrawBadge(Rectangle rectangle, Team? team, string fallback)
    {
        var primary = Theme.FromHex(team?.PrimaryColor ?? "#2E9E6B");
        Ui.Fill(rectangle, primary);
        Ui.Border(rectangle, Theme.FromHex(team?.SecondaryColor ?? "#FFFFFF"), 4);
        var shortName = team?.ShortName ?? fallback[..Math.Min(3, fallback.Length)];
        Ui.TextIn(shortName, rectangle, Theme.ContrastingText(primary), TextSize.Subhead, TextAlign.Center);
    }

    private void DrawStats(Rectangle area)
    {
        Ui.Fill(area, new Color(17, 32, 42));
        Ui.Text("MATCH STATS", new Vector2(area.X + 14, area.Y + 12), Theme.TextDim, TextSize.Small);

        var rows = new (string Label, int Home, int Away)[]
        {
            ("Possession %", _result.HomeStats.Possession, _result.AwayStats.Possession),
            ("Metres", _result.HomeStats.Metres, _result.AwayStats.Metres),
            ("Tackles", _result.HomeStats.Tackles, _result.AwayStats.Tackles),
            ("Line breaks", _result.HomeStats.LineBreaks, _result.AwayStats.LineBreaks),
            ("Errors", _result.HomeStats.Errors, _result.AwayStats.Errors),
            ("Penalties", _result.HomeStats.Penalties, _result.AwayStats.Penalties),
            ("Completion %", _result.HomeStats.CompletionRate, _result.AwayStats.CompletionRate)
        };

        var y = area.Y + 40;
        foreach (var (label, home, away) in rows)
        {
            Ui.Text(home.ToString(), new Vector2(area.X + 14, y), Theme.Text, TextSize.Small);
            Ui.TextIn(label, new Rectangle(area.X + 60, y, area.Width - 120, 22), Theme.TextMuted, TextSize.Small,
                TextAlign.Center);
            Ui.TextIn(away.ToString(), new Rectangle(area.Right - 54, y, 40, 22), Theme.Text, TextSize.Small,
                TextAlign.Right);

            var total = Math.Max(1, home + away);
            var barWidth = area.Width - 60;
            var barX = area.X + 30;
            var homeWidth = (int)(barWidth * home / (float)total);
            Ui.Fill(new Rectangle(barX, y + 22, barWidth, 5), Theme.AwayBar);
            Ui.Fill(new Rectangle(barX, y + 22, homeWidth, 5), Theme.Accent);

            y += 34;
        }
    }

    private void DrawScorersAndInjuries(Rectangle area)
    {
        Ui.Fill(area, new Color(15, 29, 38));
        Ui.Text("SCORERS", new Vector2(area.X + 14, area.Y + 12), Theme.TextDim, TextSize.Small);

        var scorers = _result.PlayerStats
            .Where(s => s.Tries > 0 || s.Goals > 0 || s.DropGoals > 0)
            .OrderByDescending(s => s.Tries * 4 + s.Goals * 2 + s.DropGoals)
            .Take(10)
            .ToList();

        var y = area.Y + 38;
        foreach (var scorer in scorers)
        {
            var bits = new List<string>();
            if (scorer.Tries > 0) bits.Add($"{scorer.Tries} try");
            if (scorer.Goals > 0) bits.Add($"{scorer.Goals} goal");
            if (scorer.DropGoals > 0) bits.Add($"{scorer.DropGoals} drop goal");

            Ui.Text(scorer.Name, new Vector2(area.X + 14, y), Theme.Text, TextSize.Small);
            Ui.TextIn(string.Join(", ", bits), new Rectangle(area.X + 190, y, area.Width - 220, 22),
                scorer.ManOfTheMatch ? Theme.Gold : Theme.TextMuted, TextSize.Small, TextAlign.Right);
            y += 24;
        }

        if (scorers.Count == 0)
            Ui.Text("No scores.", new Vector2(area.X + 14, y), Theme.TextDim, TextSize.Small);

        var man = _result.PlayerStats.FirstOrDefault(p => p.ManOfTheMatch);
        if (man is not null)
        {
            y += 8;
            Ui.Text($"Man of the match: {man.Name}", new Vector2(area.X + 14, y), Theme.Gold, TextSize.Small);
            y += 26;
        }

        if (_result.Injuries.Count > 0)
        {
            Ui.Text("INJURIES", new Vector2(area.X + 14, y + 6), Theme.TextDim, TextSize.Small);
            y += 28;
            foreach (var injury in _result.Injuries.Take(4))
            {
                Ui.Text($"{injury.PlayerName} — {injury.Type}, {injury.WeeksOut} week(s)",
                    new Vector2(area.X + 14, y), Theme.Loss, TextSize.Small);
                y += 22;
            }
        }

        if (_career is null) return;

        var otherResults = _career.PlayerLeague.FixturesForRound(_round)
            .Where(f => !f.IsBye && f.Result is not null &&
                        f.HomeTeamId != _career.PlayerTeamId && f.AwayTeamId != _career.PlayerTeamId)
            .Take(3)
            .ToList();

        if (otherResults.Count == 0 || y > area.Bottom - 100) return;

        Ui.Text("ELSEWHERE", new Vector2(area.X + 14, y + 10), Theme.TextDim, TextSize.Small);
        y += 32;
        foreach (var fixture in otherResults)
        {
            if (y > area.Bottom - 16) break;
            var home = _career.TryTeam(fixture.HomeTeamId);
            var away = _career.TryTeam(fixture.AwayTeamId);
            Ui.Text($"{home?.ShortName} {fixture.Result!.HomeScore}-{fixture.Result.AwayScore} {away?.ShortName}",
                new Vector2(area.X + 14, y), Theme.TextMuted, TextSize.Small);
            y += 22;
        }
    }
}
