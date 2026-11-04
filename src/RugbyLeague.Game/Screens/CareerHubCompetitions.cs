using Microsoft.Xna.Framework;
using RugbyLeague.Core.Model;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public sealed partial class CareerHubScreen
{
    private enum CompetitionView
    {
        Table,
        Fixtures,
        Cup,
        Finals
    }

    private CompetitionView _competitionView = CompetitionView.Table;
    private int _fixtureRound = -1;

    private void DrawCompetitions(Rectangle area)
    {
        var views = new[] { "League table", "Fixtures", "Challenge Shield", "Finals" };
        var x = area.X;
        foreach (var view in Enum.GetValues<CompetitionView>())
        {
            if (Ui.Tab(new Rectangle(x, area.Y, 170, 40), views[(int)view], _competitionView == view))
                _competitionView = view;
            x += 178;
        }

        var body = new Rectangle(area.X, area.Y + 54, area.Width, area.Height - 54);

        switch (_competitionView)
        {
            case CompetitionView.Table: DrawTable(body); break;
            case CompetitionView.Fixtures: DrawFixtures(body); break;
            case CompetitionView.Cup: DrawCup(body); break;
            case CompetitionView.Finals: DrawFinals(body); break;
        }
    }

    private void DrawTable(Rectangle area)
    {
        Ui.Card(area, new Color(13, 26, 35), Theme.PanelBorder);

        var header = new Rectangle(area.X + 14, area.Y + 12, area.Width - 28, 24);
        Ui.Text("#", new Vector2(header.X, header.Y), Theme.TextDim, TextSize.Small);
        Ui.Text("CLUB", new Vector2(header.X + 40, header.Y), Theme.TextDim, TextSize.Small);
        Ui.TextIn("P", new Rectangle(header.Right - 470, header.Y, 44, 24), Theme.TextDim, TextSize.Small, TextAlign.Center);
        Ui.TextIn("W", new Rectangle(header.Right - 426, header.Y, 44, 24), Theme.TextDim, TextSize.Small, TextAlign.Center);
        Ui.TextIn("D", new Rectangle(header.Right - 382, header.Y, 44, 24), Theme.TextDim, TextSize.Small, TextAlign.Center);
        Ui.TextIn("L", new Rectangle(header.Right - 338, header.Y, 44, 24), Theme.TextDim, TextSize.Small, TextAlign.Center);
        Ui.TextIn("+/-", new Rectangle(header.Right - 286, header.Y, 70, 24), Theme.TextDim, TextSize.Small, TextAlign.Center);
        Ui.TextIn("PTS", new Rectangle(header.Right - 206, header.Y, 56, 24), Theme.TextDim, TextSize.Small, TextAlign.Center);
        Ui.TextIn("FORM", new Rectangle(header.Right - 142, header.Y, 120, 24), Theme.TextDim, TextSize.Small, TextAlign.Center);

        var table = _career.TableFor(League);
        var y = area.Y + 46;
        var rowHeight = 34;
        var rank = 1;

        foreach (var row in table)
        {
            if (y + rowHeight > area.Bottom - 10) break;

            var isPlayer = row.TeamId == _career.PlayerTeamId;
            var inFinals = rank <= League.FinalsSpots;
            var relegation = League.HasPromotionRelegation && rank > table.Count - League.RelegationSpots;

            var rectangle = new Rectangle(area.X + 8, y, area.Width - 16, rowHeight);
            if (isPlayer) Ui.Fill(rectangle, new Color(32, 62, 56));
            else if (rank % 2 == 0) Ui.Fill(rectangle, new Color(18, 32, 42));

            if (inFinals) Ui.Fill(new Rectangle(rectangle.X, rectangle.Y, 3, rectangle.Height), Theme.Accent);
            else if (relegation) Ui.Fill(new Rectangle(rectangle.X, rectangle.Y, 3, rectangle.Height), Theme.Loss);

            Ui.Text(rank.ToString(), new Vector2(rectangle.X + 12, rectangle.Y + 8), Theme.TextMuted, TextSize.Small);

            var team = _career.TryTeam(row.TeamId);
            if (team is not null)
                Ui.Fill(new Rectangle(rectangle.X + 34, rectangle.Y + 12, 8, 10), Theme.FromHex(team.PrimaryColor));

            Ui.Text(row.TeamName, new Vector2(rectangle.X + 50, rectangle.Y + 8),
                isPlayer ? Theme.Text : Theme.TextMuted, TextSize.Small);

            Ui.TextIn(row.Played.ToString(), new Rectangle(rectangle.Right - 470, rectangle.Y, 44, rowHeight),
                Theme.TextMuted, TextSize.Small, TextAlign.Center);
            Ui.TextIn(row.Won.ToString(), new Rectangle(rectangle.Right - 426, rectangle.Y, 44, rowHeight),
                Theme.TextMuted, TextSize.Small, TextAlign.Center);
            Ui.TextIn(row.Drawn.ToString(), new Rectangle(rectangle.Right - 382, rectangle.Y, 44, rowHeight),
                Theme.TextMuted, TextSize.Small, TextAlign.Center);
            Ui.TextIn(row.Lost.ToString(), new Rectangle(rectangle.Right - 338, rectangle.Y, 44, rowHeight),
                Theme.TextMuted, TextSize.Small, TextAlign.Center);
            Ui.TextIn(row.Difference > 0 ? $"+{row.Difference}" : row.Difference.ToString(),
                new Rectangle(rectangle.Right - 286, rectangle.Y, 70, rowHeight), Theme.TextMuted, TextSize.Small,
                TextAlign.Center);
            Ui.TextIn(row.Points.ToString(), new Rectangle(rectangle.Right - 206, rectangle.Y, 56, rowHeight),
                Theme.Text, TextSize.Small, TextAlign.Center);

            var formX = rectangle.Right - 138;
            foreach (var form in row.Form.TakeLast(5))
            {
                var color = form == "W" ? Theme.Win : form == "D" ? Theme.Draw : Theme.Loss;
                var box = new Rectangle(formX, rectangle.Y + 9, 20, 16);
                Ui.Fill(box, color);
                Ui.TextIn(form, box, Theme.ContrastingText(color), TextSize.Small, TextAlign.Center);
                formX += 22;
            }

            y += rowHeight;
            rank++;
        }

        Ui.Text("Green: finals places    Red: relegation places", new Vector2(area.X + 16, area.Bottom - 26),
            Theme.TextDim, TextSize.Small);
    }

    private void DrawFixtures(Rectangle area)
    {
        Ui.Card(area, new Color(13, 26, 35), Theme.PanelBorder);

        if (_fixtureRound < 0) _fixtureRound = Math.Clamp(_career.Round, 0, Math.Max(0, League.Rounds - 1));

        var header = new Rectangle(area.X + 16, area.Y + 12, area.Width - 32, 44);
        Ui.Text($"Round {_fixtureRound + 1} of {League.Rounds}", new Vector2(header.X, header.Y + 10),
            Theme.Text, TextSize.Subhead);

        if (Ui.Button(new Rectangle(header.Right - 210, header.Y + 2, 96, 38), "Previous", ButtonStyle.Ghost))
            _fixtureRound = Math.Max(0, _fixtureRound - 1);
        if (Ui.Button(new Rectangle(header.Right - 106, header.Y + 2, 96, 38), "Next", ButtonStyle.Ghost))
            _fixtureRound = Math.Min(League.Rounds - 1, _fixtureRound + 1);

        var fixtures = League.FixturesForRound(_fixtureRound).Where(f => !f.IsBye).ToList();
        var y = area.Y + 68;
        var rowHeight = 42;

        foreach (var fixture in fixtures)
        {
            if (y + rowHeight > area.Bottom - 12) break;

            var home = _career.TeamById(fixture.HomeTeamId);
            var away = _career.TeamById(fixture.AwayTeamId);
            var isPlayer = fixture.HomeTeamId == _career.PlayerTeamId || fixture.AwayTeamId == _career.PlayerTeamId;

            var rectangle = new Rectangle(area.X + 12, y, area.Width - 24, rowHeight - 4);
            Ui.Fill(rectangle, isPlayer ? new Color(32, 62, 56) : new Color(17, 31, 41));

            Ui.Fill(new Rectangle(rectangle.X + 10, rectangle.Y + 15, 8, 10), Theme.FromHex(home.PrimaryColor));
            Ui.Text(home.Name, new Vector2(rectangle.X + 28, rectangle.Y + 10), Theme.TextMuted, TextSize.Small);

            Ui.TextIn(fixture.IsPlayed ? fixture.Result!.ScoreLine : "v",
                new Rectangle(rectangle.X + rectangle.Width / 2 - 60, rectangle.Y, 120, rectangle.Height),
                fixture.IsPlayed ? Theme.Text : Theme.TextDim, TextSize.Small, TextAlign.Center);

            Ui.Fill(new Rectangle(rectangle.Right - 18, rectangle.Y + 15, 8, 10), Theme.FromHex(away.PrimaryColor));
            Ui.TextIn(away.Name, new Rectangle(rectangle.X + rectangle.Width / 2 + 66, rectangle.Y,
                rectangle.Width / 2 - 100, rectangle.Height), Theme.TextMuted, TextSize.Small, TextAlign.Right);

            y += rowHeight;
        }

        if (fixtures.Count == 0)
            Ui.Text("No fixtures scheduled for this round.", new Vector2(area.X + 16, area.Y + 80),
                Theme.TextDim, TextSize.Small);
    }

    private void DrawCup(Rectangle area)
    {
        DrawKnockout(area, _career.Cup);
    }

    private void DrawFinals(Rectangle area)
    {
        var finals = _career.FinalsSeries.FirstOrDefault(c => c.LeagueId == League.Id);
        if (finals is null)
        {
            Ui.Card(area, new Color(13, 26, 35), Theme.PanelBorder);
            Ui.Text("Finals begin once the league rounds are complete.", new Vector2(area.X + 20, area.Y + 20),
                Theme.TextMuted, TextSize.Body);
            return;
        }

        DrawKnockout(area, finals);
    }

    private void DrawKnockout(Rectangle area, Cup cup)
    {
        Ui.Card(area, new Color(13, 26, 35), Theme.PanelBorder);

        Ui.Text(cup.Name, new Vector2(area.X + 16, area.Y + 14), Theme.Text, TextSize.Subhead);
        Ui.Text(cup.IsComplete
                ? $"Winners: {_career.TryTeam(cup.WinnerId!.Value)?.Name ?? "n/a"}"
                : $"Prize money {Theme.Money(cup.PrizeMoney)}  ·  {cup.Rounds.Count} rounds",
            new Vector2(area.X + 16, area.Y + 46), cup.IsComplete ? Theme.Gold : Theme.TextMuted, TextSize.Small);

        var y = area.Y + 78;
        var rounds = cup.Rounds;
        var columnWidth = (area.Width - 32) / Math.Max(1, Math.Min(3, rounds.Count));
        var column = 0;
        var columnY = y;
        var columnLimit = 3;

        foreach (var round in rounds)
        {
            if (round.IsEmpty) continue;

            if (column >= columnLimit)
            {
                column = 0;
                columnY += 210;
                columnWidth = (area.Width - 32);
                columnLimit = 1;
            }

            var x = area.X + 16 + column * columnWidth;
            var top = columnY;
            var isCurrent = !cup.IsComplete && cup.CurrentRound == round;

            Ui.Text(round.Name + (isCurrent ? "  ·  NEXT" : ""), new Vector2(x, top),
                isCurrent ? Theme.Gold : Theme.TextMuted, TextSize.Small);

            var rowY = top + 24;
            foreach (var fixture in round.Fixtures)
            {
                if (rowY + 24 > area.Bottom - 12) break;

                var home = _career.TryTeam(fixture.HomeTeamId);
                var away = _career.TryTeam(fixture.AwayTeamId);
                var playerInvolved = fixture.HomeTeamId == _career.PlayerTeamId ||
                                     fixture.AwayTeamId == _career.PlayerTeamId;

                var label = fixture.IsPlayed
                    ? $"{home?.ShortName} {fixture.Result!.HomeScore} - {fixture.Result.AwayScore} {away?.ShortName}"
                    : $"{home?.ShortName} v {away?.ShortName}";

                Ui.Text(label, new Vector2(x + 6, rowY), playerInvolved ? Theme.Text : Theme.TextDim, TextSize.Small);
                rowY += 22;
            }

            if (round.Byes.Count > 0 && rowY + 20 < area.Bottom - 12)
                Ui.Text($"{round.Byes.Count} byes", new Vector2(x + 6, rowY + 4), Theme.TextDim, TextSize.Small);

            column++;
        }
    }
}
