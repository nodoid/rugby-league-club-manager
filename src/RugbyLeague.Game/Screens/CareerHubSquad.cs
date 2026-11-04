using Microsoft.Xna.Framework;
using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Model;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public sealed partial class CareerHubScreen
{
    private enum SquadFilter
    {
        All,
        Backs,
        Forwards,
        Injured
    }

    private SquadFilter _squadFilter = SquadFilter.All;
    private float _squadScroll;

    private void DrawSquad(Rectangle area)
    {
        var header = new Rectangle(area.X, area.Y, area.Width, 52);
        Ui.Text($"Squad ({Club.Squad.Count})", new Vector2(header.X, header.Y + 4), Theme.Text, TextSize.Subhead);
        Ui.Text($"Rating {Club.SquadRating}  ·  Wage bill {Theme.Money(Club.WeeklyWageBill)}/wk  ·  Cap space {Theme.Money(_career.CapSpace)}",
            new Vector2(header.X + 200, header.Y + 10), Theme.TextMuted, TextSize.Small);

        if (Ui.Button(new Rectangle(header.Right - 210, header.Y, 200, 44), "Select best XVII",
                ButtonStyle.Primary))
        {
            var sheet = Club.SelectBestXV();
            _career.LineupStarterIds = sheet.Starters.Select(p => p.Id).ToList();
            _career.LineupBenchIds = sheet.Bench.Select(p => p.Id).ToList();
            Game.ShowNotice("Selection saved. The strongest available XVII will start.");
        }

        var filtersY = area.Y + 62;
        var x = area.X;
        foreach (var filter in Enum.GetValues<SquadFilter>())
        {
            if (Ui.Tab(new Rectangle(x, filtersY, 116, 36), filter.ToString(), _squadFilter == filter))
            {
                _squadFilter = filter;
                _squadScroll = 0;
            }

            x += 124;
        }

        var list = new Rectangle(area.X, filtersY + 46, area.Width, area.Bottom - (filtersY + 46));
        Ui.Card(list, new Color(13, 26, 35), Theme.PanelBorder);

        var players = Club.Squad
            .Where(p => _squadFilter switch
            {
                SquadFilter.Backs => p.Position is PlayerPosition.Fullback or PlayerPosition.Wing or PlayerPosition.Centre
                    or PlayerPosition.FiveEighth or PlayerPosition.Halfback,
                SquadFilter.Forwards => p.Position is PlayerPosition.Prop or PlayerPosition.Hooker
                    or PlayerPosition.SecondRow or PlayerPosition.Lock,
                SquadFilter.Injured => p.IsInjured,
                _ => true
            })
            .OrderByDescending(p => p.Rating)
            .ThenBy(p => p.ShirtNumber)
            .ToList();

        var rowHeight = 46;
        var contentHeight = players.Count * rowHeight + 8;
        _squadScroll = Ui.HandleScroll(_squadScroll, list, contentHeight);

        var y = list.Y + 6 - (int)_squadScroll;
        foreach (var player in players)
        {
            if (y + rowHeight <= list.Bottom && y >= list.Y)
            {
                var row = new Rectangle(list.X + 6, y, list.Width - 22, rowHeight - 4);
                PlayerRow(row, player);

                if (Ui.Tapped(row))
                    Game.Screens.Push(new PlayerScreen(Game, player, PlayerScreenMode.Squad));
            }

            y += rowHeight;
        }

        Ui.ScrollIndicator(list, _squadScroll, contentHeight);
    }

    private void DrawTraining(Rectangle area)
    {
        var leftWidth = (int)(area.Width * 0.56f);
        var left = new Rectangle(area.X, area.Y, leftWidth, area.Height);
        var right = new Rectangle(left.Right + 16, area.Y, area.Width - leftWidth - 16, area.Height);

        var focusCard = new Rectangle(left.X, left.Y, left.Width, 210);
        Ui.Card(focusCard, new Color(16, 32, 42), Theme.PanelBorder);
        Ui.Text("TRAINING FOCUS", new Vector2(focusCard.X + 16, focusCard.Y + 14), Theme.TextDim, TextSize.Small);
        Ui.Text("The weekly focus shapes which attributes develop for every fit player.",
            new Vector2(focusCard.X + 16, focusCard.Y + 36), Theme.TextMuted, TextSize.Small);

        var focuses = Enum.GetValues<TrainingFocus>();
        var buttonWidth = (focusCard.Width - 32 - 12) / 3;
        for (var i = 0; i < focuses.Length; i++)
        {
            var focus = focuses[i];
            var column = i % 3;
            var row = i / 3;
            var rectangle = new Rectangle(focusCard.X + 16 + column * (buttonWidth + 6),
                focusCard.Y + 66 + row * 44, buttonWidth, 36);

            if (Ui.Toggle(rectangle, focus.ToString(), Club.TrainingFocus == focus))
            {
                Club.TrainingFocus = focus;
                Game.ShowNotice($"Training focus set to {focus}.");
            }
        }

        if (Ui.Button(new Rectangle(focusCard.X + 16, focusCard.Y + 156, focusCard.Width - 32, 38),
                $"Extra goal-kicking session  ·  {Theme.ShortMoney(_career.KickingSessionCost)}", ButtonStyle.Gold,
                _career.Balance >= _career.KickingSessionCost))
        {
            var (success, message) = _career.RunKickingSession();
            Game.ShowNotice(message);
            if (success) Game.SaveCareer();
        }

        var facilityCard = new Rectangle(left.X, focusCard.Bottom + 10, left.Width, 150);
        Ui.Card(facilityCard, new Color(16, 32, 42), Theme.PanelBorder);
        Ui.Text("FACILITIES", new Vector2(facilityCard.X + 16, facilityCard.Y + 12), Theme.TextDim, TextSize.Small);

        Ui.Text($"Training base  ·  L{Club.TrainingFacility}/10", new Vector2(facilityCard.X + 16, facilityCard.Y + 36),
            Theme.Text, TextSize.Small);
        Ui.Text($"Youth academy  ·  L{Club.YouthAcademy}/10", new Vector2(facilityCard.X + 16, facilityCard.Y + 84),
            Theme.Text, TextSize.Small);

        if (Ui.Button(new Rectangle(facilityCard.Right - 350, facilityCard.Y + 30, 166, 34),
                $"Upgrade {Theme.ShortMoney(_career.TrainingUpgradeCost)}", ButtonStyle.Gold,
                Club.TrainingFacility < 10 && _career.Balance >= _career.TrainingUpgradeCost))
        {
            var (success, message) = _career.UpgradeTrainingFacility();
            Game.ShowNotice(message);
            if (success) Game.SaveCareer();
        }

        if (Ui.Button(new Rectangle(facilityCard.Right - 350, facilityCard.Y + 78, 166, 34),
                $"Upgrade {Theme.ShortMoney(_career.AcademyUpgradeCost)}", ButtonStyle.Gold,
                Club.YouthAcademy < 10 && _career.Balance >= _career.AcademyUpgradeCost))
        {
            var (success, message) = _career.UpgradeYouthAcademy();
            Game.ShowNotice(message);
            if (success) Game.SaveCareer();
        }

        if (Ui.Button(new Rectangle(facilityCard.Right - 172, facilityCard.Y + 30, 152, 34), "Ledger", ButtonStyle.Ghost))
            _tab = HubTab.Club;

        var condition = new Rectangle(left.X, facilityCard.Bottom + 10, left.Width, left.Bottom - facilityCard.Bottom - 10);
        Ui.Card(condition, new Color(16, 32, 42), Theme.PanelBorder);
        Ui.Text("SQUAD CONDITION", new Vector2(condition.X + 16, condition.Y + 12), Theme.TextDim, TextSize.Small);

        var averageFitness = Club.Squad.Count == 0 ? 0 : (int)Club.Squad.Average(p => p.Fitness);
        var averageMorale = Club.Squad.Count == 0 ? 0 : (int)Club.Squad.Average(p => p.Morale);
        var tired = Club.Squad.Count(p => p.Fitness < 60);

        Ui.Text($"Average fitness {averageFitness}%", new Vector2(condition.X + 16, condition.Y + 40), Theme.Text,
            TextSize.Small);
        Ui.ProgressBar(new Rectangle(condition.X + 16, condition.Y + 62, condition.Width - 32, 8), averageFitness / 100f,
            averageFitness > 75 ? Theme.Accent : Theme.Gold);
        Ui.Text($"Average morale {averageMorale}%", new Vector2(condition.X + 16, condition.Y + 82), Theme.Text,
            TextSize.Small);
        Ui.ProgressBar(new Rectangle(condition.X + 16, condition.Y + 104, condition.Width - 32, 8), averageMorale / 100f,
            averageMorale > 60 ? Theme.Accent : Theme.Danger);
        Ui.Text(Ui.Ellipsis($"{tired} player(s) below 60% condition  ·  recovery improves with the training base.",
                TextSize.Small, condition.Width - 32),
            new Vector2(condition.X + 16, condition.Y + 124), Theme.TextMuted, TextSize.Small);

        var injured = new Rectangle(right.X, right.Y, right.Width, right.Height);
        Ui.Card(injured, new Color(13, 26, 35), Theme.PanelBorder);
        Ui.Text($"TREATMENT ROOM ({Club.Squad.Count(p => p.IsInjured)})",
            new Vector2(injured.X + 16, injured.Y + 12), Theme.TextDim, TextSize.Small);

        var y = injured.Y + 42;
        var rowHeight = 48;
        foreach (var player in Club.Squad.Where(p => p.IsInjured).OrderBy(p => p.Injury!.WeeksRemaining))
        {
            if (y + rowHeight > injured.Bottom - 10) break;

            Ui.Fill(new Rectangle(injured.X + 8, y, injured.Width - 16, rowHeight - 4), new Color(26, 34, 40));
            Ui.Text(player.Name, new Vector2(injured.X + 20, y + 6), Theme.Text, TextSize.Small);
            Ui.Text($"{player.Injury!.Type}  ·  {player.Injury.WeeksRemaining} week(s)", new Vector2(injured.X + 20, y + 24),
                Theme.Loss, TextSize.Small);
            Ui.TextIn($"{player.Position}", new Rectangle(injured.Right - 130, y, 110, rowHeight), Theme.TextDim,
                TextSize.Small, TextAlign.Right);
            y += rowHeight;
        }

        if (!Club.Squad.Any(p => p.IsInjured))
            Ui.Text("No injuries. The full squad is available.", new Vector2(injured.X + 16, injured.Y + 46),
                Theme.TextMuted, TextSize.Small);
    }
}
