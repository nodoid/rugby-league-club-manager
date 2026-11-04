using Microsoft.Xna.Framework;
using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Model;
using RugbyLeague.Core.Sim;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public sealed partial class CareerHubScreen
{
    private void DrawHome(Rectangle area)
    {
        var leftWidth = (int)(area.Width * 0.61f);
        var left = new Rectangle(area.X, area.Y, leftWidth, area.Height);
        var right = new Rectangle(left.Right + 16, area.Y, area.Width - leftWidth - 16, area.Height);

        if (_career.Phase == CompetitionPhase.OffSeason)
        {
            DrawSeasonReview(left);
        }
        else
        {
            DrawNextMatch(left);
            DrawNews(new Rectangle(left.X, left.Y + 246, left.Width, left.Height - 246));
        }

        DrawClubSnapshot(right);
    }

    private void DrawNextMatch(Rectangle area)
    {
        var card = new Rectangle(area.X, area.Y, area.Width, 232);
        Ui.Card(card, new Color(16, 32, 42), Theme.PanelBorder);

        var pending = _career.PendingMatch;

        if (pending is null)
        {
            Ui.Text("Bye week", new Vector2(card.X + 20, card.Y + 18), Theme.Gold, TextSize.Subhead);
            Ui.Text("No fixture for your club this round. Use the week to train and recover.",
                new Vector2(card.X + 20, card.Y + 54), Theme.TextMuted, TextSize.Small);
            if (Ui.Button(new Rectangle(card.X + 20, card.Bottom - 62, 180, 44), "Advance week",
                    ButtonStyle.Primary))
                AdvanceWeek();
            return;
        }

        var fixture = pending.Fixture;
        var opponentId = fixture.HomeTeamId == _career.PlayerTeamId ? fixture.AwayTeamId : fixture.HomeTeamId;
        var opponent = _career.TeamById(opponentId);
        var atHome = fixture.HomeTeamId == _career.PlayerTeamId;

        var textWidth = card.Width - 210;
        Ui.Text(Ui.Ellipsis($"{pending.CompetitionName.ToUpperInvariant()}  ·  {(fixture.Round > 0 ? $"ROUND {fixture.Round}" : "KNOCKOUT")}",
                TextSize.Small, textWidth), new Vector2(card.X + 20, card.Y + 14), Theme.Gold, TextSize.Small);

        Ui.Text(atHome ? "vs" : "away at", new Vector2(card.X + 20, card.Y + 40), Theme.TextMuted, TextSize.Small);
        Ui.Text(Ui.Ellipsis(opponent.Name, TextSize.Heading, card.Width - 210),
            new Vector2(card.X + 20, card.Y + 58), Theme.Text, TextSize.Heading);

        var venue = atHome ? Club.StadiumName : opponent.StadiumName;
        var weather = _career.WeatherForRound(_career.Round);
        Ui.Text(Ui.Ellipsis($"{venue}  ·  {weather.Label}  ·  {weather.Temperature}°C", TextSize.Small, textWidth),
            new Vector2(card.X + 20, card.Y + 112), Theme.TextMuted, TextSize.Small);

        var form = opponent.Reputation >= 80 ? "Title contender" : opponent.Reputation >= 72 ? "Playoff hopeful" : "Improving side";
        Ui.Text(Ui.Ellipsis($"{form}  ·  Squad rating {opponent.SquadRating}", TextSize.Small, textWidth),
            new Vector2(card.X + 20, card.Y + 136), Theme.TextDim, TextSize.Small);

        var buttonY = card.Bottom - 62;
        if (Ui.Button(new Rectangle(card.X + 20, buttonY, 168, 44), "Matchday", ButtonStyle.Primary))
            Game.Screens.Push(new MatchdayScreen(Game));

        if (Ui.Button(new Rectangle(card.X + 198, buttonY, 132, 44), "Quick result", ButtonStyle.Ghost))
            QuickResult();

        var crest = new Rectangle(card.Right - 150, card.Y + 26, 120, 120);
        Ui.Fill(crest, Theme.FromHex(opponent.PrimaryColor));
        Ui.Border(crest, Theme.FromHex(opponent.SecondaryColor), 4);
        Ui.TextIn(opponent.ShortName, crest, Theme.ContrastingText(Theme.FromHex(opponent.PrimaryColor)),
            TextSize.Heading, TextAlign.Center);
    }

    private void QuickResult()
    {
        var setup = _career.PendingSetup(false);
        if (setup is null) return;

        var round = _career.Round;
        var result = MatchEngine.Simulate(setup);
        _career.SubmitPendingMatch(result);
        Game.SaveCareer();
        Game.Screens.Push(new PostMatchScreen(Game, result, round));
    }

    private void DrawNews(Rectangle area)
    {
        Ui.Card(area, new Color(13, 26, 35), Theme.PanelBorder);
        Ui.Text("CLUB NEWS", new Vector2(area.X + 16, area.Y + 12), Theme.TextMuted, TextSize.Small);

        var y = area.Y + 40;
        var rowHeight = 62;
        var maxRows = (area.Height - 50) / rowHeight;

        foreach (var item in _career.News.Take(maxRows))
        {
            var row = new Rectangle(area.X + 10, y, area.Width - 20, rowHeight);
            if (item.IsRead) Ui.Fill(new Rectangle(row.X, row.Bottom - 1, row.Width, 1), Theme.PanelBorder);
            else Ui.Fill(new Rectangle(row.X, row.Y + 10, 4, row.Height - 20), Theme.Gold);

            var headlineColor = item.IsRead ? Theme.TextMuted : Theme.Text;
            Ui.Text(Ui.Ellipsis(item.Headline, TextSize.Small, row.Width - 28),
                new Vector2(row.X + 14, row.Y + 6), headlineColor, TextSize.Small);

            Ui.Text(Ui.Ellipsis(item.Body, TextSize.Small, row.Width - 28),
                new Vector2(row.X + 14, row.Y + 28), Theme.TextDim, TextSize.Small);
            y += rowHeight;

            if (y > area.Bottom - rowHeight) break;
        }

        _career.MarkNewsRead();
    }

    private void DrawClubSnapshot(Rectangle area)
    {
        var table = _career.TableFor(League);
        var position = _career.PlayerLeaguePosition();
        var row = table.FirstOrDefault(r => r.TeamId == _career.PlayerTeamId);
        var injuries = Club.Squad.Count(p => p.IsInjured);

        var tileWidth = (area.Width - 12) / 2;
        var tileHeight = 84;

        StatTile(new Rectangle(area.X, area.Y, tileWidth, tileHeight), "League position",
            $"{position}{Ordinal(position)}", position <= League.FinalsSpots ? Theme.Accent : Theme.Gold);
        StatTile(new Rectangle(area.X + tileWidth + 12, area.Y, tileWidth, tileHeight), "League points",
            row?.Points.ToString() ?? "0", Theme.Gold);
        StatTile(new Rectangle(area.X, area.Y + tileHeight + 12, tileWidth, tileHeight), "Squad rating",
            Club.SquadRating.ToString(), Theme.Accent);
        StatTile(new Rectangle(area.X + tileWidth + 12, area.Y + tileHeight + 12, tileWidth, tileHeight),
            "Injuries", $"{injuries} out", injuries == 0 ? Theme.Win : Theme.Loss);

        var formCard = new Rectangle(area.X, area.Y + (tileHeight + 12) * 2, area.Width, 116);
        Ui.Card(formCard, new Color(16, 32, 42), Theme.PanelBorder, shadow: false);
        Ui.Text("FORM", new Vector2(formCard.X + 14, formCard.Y + 12), Theme.TextDim, TextSize.Small);

        var form = row?.Form ?? new List<string>();
        if (form.Count == 0)
        {
            Ui.Text("No matches played yet.", new Vector2(formCard.X + 14, formCard.Y + 40), Theme.TextMuted,
                TextSize.Small);
        }
        else
        {
            var x = formCard.X + 14;
            foreach (var entry in form.TakeLast(8))
            {
                var colour = entry == "W" ? Theme.Win : entry == "D" ? Theme.Draw : Theme.Loss;
                var box = new Rectangle(x, formCard.Y + 38, 30, 30);
                Ui.Fill(box, colour);
                Ui.TextIn(entry, box, Theme.ContrastingText(colour), TextSize.Small, TextAlign.Center);
                x += 36;
            }
        }

        Ui.Text(Ui.Ellipsis($"Board target: top-half  ·  {(position <= League.FinalsSpots ? "on track" : "work to do")}",
                TextSize.Small, formCard.Width - 28),
            new Vector2(formCard.X + 14, formCard.Y + 84),
            position <= League.FinalsSpots ? Theme.TextMuted : Theme.Gold, TextSize.Small);

        var nextCard = new Rectangle(area.X, formCard.Bottom + 12, area.Width, area.Bottom - formCard.Bottom - 12);
        if (nextCard.Height < 90) return;

        Ui.Card(nextCard, new Color(16, 32, 42), Theme.PanelBorder, shadow: false);
        Ui.Text("TOP SCORER", new Vector2(nextCard.X + 14, nextCard.Y + 12), Theme.TextDim, TextSize.Small);

        var topScorer = Club.Squad
            .OrderByDescending(p => p.Season.Points)
            .ThenByDescending(p => p.Season.Tries)
            .FirstOrDefault();

        if (topScorer is null)
        {
            Ui.Text("No appearances yet.", new Vector2(nextCard.X + 14, nextCard.Y + 40), Theme.TextMuted,
                TextSize.Small);
        }
        else
        {
            Ui.Text(topScorer.Name, new Vector2(nextCard.X + 14, nextCard.Y + 40), Theme.Text, TextSize.Body);
            Ui.Text($"{topScorer.Season.Points} pts  ·  {topScorer.Season.Tries} tries  ·  {topScorer.Season.Metres} m",
                new Vector2(nextCard.X + 14, nextCard.Y + 70), Theme.TextMuted, TextSize.Small);
        }
    }

    private void DrawSeasonReview(Rectangle area)
    {
        var card = new Rectangle(area.X, area.Y, area.Width, area.Height);
        Ui.Card(card, new Color(16, 32, 42), Theme.PanelBorder);
        Ui.Text($"{_career.SeasonYear} season review", new Vector2(card.X + 22, card.Y + 20), Theme.Gold,
            TextSize.Subhead);

        var summary = _career.LastSeason;
        var position = _career.PlayerLeaguePosition();

        Ui.Text($"Final league position: {position}{Ordinal(position)}  ·  {League.Name}",
            new Vector2(card.X + 22, card.Y + 66), Theme.Text, TextSize.Body);
        Ui.Text($"Balance {Theme.Money(_career.Balance)}  ·  Squad rating {Club.SquadRating}",
            new Vector2(card.X + 22, card.Y + 98), Theme.TextMuted, TextSize.Small);

        if (summary is not null)
        {
            var champion = summary.Leagues.FirstOrDefault(l => l.LeagueName == League.Name)?.Champion ?? "n/a";
            Ui.Text($"Champions: {champion}", new Vector2(card.X + 22, card.Y + 128),
                Theme.TextMuted, TextSize.Small);
            Ui.Text($"Challenge Shield: {summary.CupWinner}", new Vector2(card.X + 22, card.Y + 152),
                Theme.TextMuted, TextSize.Small);

            if (summary.PlayerClubPromoted)
                Ui.Text("Promoted!", new Vector2(card.X + 22, card.Y + 176), Theme.Win, TextSize.Small);
            else if (summary.PlayerClubRelegated)
                Ui.Text("Relegated.", new Vector2(card.X + 22, card.Y + 176), Theme.Loss, TextSize.Small);
        }

        if (Ui.Button(new Rectangle(card.X + 22, card.Bottom - 70, 220, 48), "Start next season",
                ButtonStyle.Primary))
        {
            _career.BeginNewSeason();
            Game.SaveCareer();
            Game.ShowNotice($"Welcome to the {_career.SeasonYear} season.");
            Game.Screens.ReplaceAll(new CareerHubScreen(Game));
        }
    }

    private static string Ordinal(int value)
    {
        var suffix = value % 100 is >= 11 and <= 13
            ? "th"
            : (value % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
        return suffix;
    }
}
