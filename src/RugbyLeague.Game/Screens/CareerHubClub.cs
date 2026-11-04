using Microsoft.Xna.Framework;
using RugbyLeague.Core.Model;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public sealed partial class CareerHubScreen
{
    private void DrawClub(Rectangle area)
    {
        var tileWidth = (area.Width - 36) / 4;
        const int tileHeight = 84;

        StatTile(new Rectangle(area.X, area.Y, tileWidth, tileHeight), "Bank balance",
            Theme.Money(_career.Balance), _career.Balance >= 0 ? Theme.Accent : Theme.Loss);
        StatTile(new Rectangle(area.X + tileWidth + 12, area.Y, tileWidth, tileHeight), "Weekly wage bill",
            Theme.Money(Club.WeeklyWageBill), Theme.Gold);
        StatTile(new Rectangle(area.X + (tileWidth + 12) * 2, area.Y, tileWidth, tileHeight), "Salary cap space",
            Theme.Money(_career.CapSpace), _career.CapSpace >= 0 ? Theme.Accent : Theme.Loss);
        StatTile(new Rectangle(area.X + (tileWidth + 12) * 3, area.Y, tileWidth, tileHeight), "Transfer budget",
            Theme.Money(Club.Finances.TransferBudget), Theme.Gold);

        var leftWidth = (int)(area.Width * 0.44f);
        var left = new Rectangle(area.X, area.Y + tileHeight + 12, leftWidth, area.Height - tileHeight - 12);
        var right = new Rectangle(left.Right + 16, left.Y, area.Width - leftWidth - 16, left.Height);

        DrawLedger(new Rectangle(left.X, left.Y, left.Width, 190));
        DrawFacilities(new Rectangle(left.X, left.Y + 202, left.Width, left.Height - 202));
        // Offers get the larger share: three of them can be on the table at once.
        DrawSponsors(new Rectangle(right.X, right.Y, right.Width, (int)(right.Height * 0.42f)));
        DrawSponsorOffers(new Rectangle(right.X, right.Y + (int)(right.Height * 0.42f) + 12, right.Width,
            right.Height - (int)(right.Height * 0.42f) - 12));
    }

    private void DrawLedger(Rectangle area)
    {
        Ui.Card(area, new Color(16, 32, 42), Theme.PanelBorder);
        Ui.Text("SEASON LEDGER", new Vector2(area.X + 16, area.Y + 12), Theme.TextDim, TextSize.Small);

        var revenue = Club.Finances.SeasonRevenue;
        var costs = Club.Finances.SeasonCosts;

        Ui.Text("Revenue", new Vector2(area.X + 16, area.Y + 40), Theme.TextMuted, TextSize.Small);
        Ui.TextIn(Theme.Money(revenue), new Rectangle(area.Right - 200, area.Y + 36, 180, 24), Theme.Win,
            TextSize.Small, TextAlign.Right);

        Ui.Text("Costs", new Vector2(area.X + 16, area.Y + 68), Theme.TextMuted, TextSize.Small);
        Ui.TextIn(Theme.Money(costs), new Rectangle(area.Right - 200, area.Y + 64, 180, 24), Theme.Loss,
            TextSize.Small, TextAlign.Right);

        Ui.Divider(new Rectangle(area.X + 16, area.Y + 96, area.Width - 32, 1));

        Ui.Text("Matchday ticket price", new Vector2(area.X + 16, area.Y + 108), Theme.TextMuted, TextSize.Small);
        Ui.TextIn(Theme.Money(Club.Finances.TicketPrice), new Rectangle(area.Right - 88, area.Y + 102, 40, 26),
            Theme.Text, TextSize.Small, TextAlign.Right);

        if (Ui.Button(new Rectangle(area.Right - 44, area.Y + 102, 28, 26), "-", ButtonStyle.Ghost))
            Club.Finances.TicketPrice = Math.Max(10, Club.Finances.TicketPrice - 2);

        if (Ui.Button(new Rectangle(area.Right - 16, area.Y + 102, 28, 26), "+", ButtonStyle.Ghost))
            Club.Finances.TicketPrice = Math.Min(90, Club.Finances.TicketPrice + 2);

        Ui.Text("Season tickets", new Vector2(area.X + 16, area.Y + 140), Theme.TextMuted, TextSize.Small);
        Ui.TextIn($"{Club.Finances.SeasonTicketHolders:N0} holders", new Rectangle(area.Right - 220, area.Y + 136, 200, 24),
            Theme.Text, TextSize.Small, TextAlign.Right);

        Ui.Text($"Sponsorship income {Theme.Money(Club.AnnualSponsorship)}/season",
            new Vector2(area.X + 16, area.Y + 164), Theme.TextDim, TextSize.Small);
    }

    private void DrawFacilities(Rectangle area)
    {
        Ui.Card(area, new Color(16, 32, 42), Theme.PanelBorder);
        Ui.Text("FACILITIES & ACADEMY", new Vector2(area.X + 16, area.Y + 12), Theme.TextDim, TextSize.Small);

        Ui.Text($"Training base  ·  level {Club.TrainingFacility}/10", new Vector2(area.X + 16, area.Y + 40),
            Theme.Text, TextSize.Small);
        Ui.ProgressBar(new Rectangle(area.X + 16, area.Y + 62, area.Width - 32, 8), Club.TrainingFacility / 10f,
            Theme.Accent);

        Ui.Text($"Youth academy  ·  level {Club.YouthAcademy}/10", new Vector2(area.X + 16, area.Y + 86),
            Theme.Text, TextSize.Small);
        Ui.ProgressBar(new Rectangle(area.X + 16, area.Y + 108, area.Width - 32, 8), Club.YouthAcademy / 10f,
            Theme.Gold);

        if (Ui.Button(new Rectangle(area.X + 16, area.Y + 132, (area.Width - 44) / 2, 38),
                $"Train {Theme.ShortMoney(_career.TrainingUpgradeCost)}", ButtonStyle.Normal,
                Club.TrainingFacility < 10 && _career.Balance >= _career.TrainingUpgradeCost))
        {
            var (success, message) = _career.UpgradeTrainingFacility();
            Game.ShowNotice(message);
            if (success) Game.SaveCareer();
        }

        if (Ui.Button(new Rectangle(area.X + 28 + (area.Width - 44) / 2, area.Y + 132, (area.Width - 44) / 2, 38),
                $"Academy {Theme.ShortMoney(_career.AcademyUpgradeCost)}", ButtonStyle.Normal,
                Club.YouthAcademy < 10 && _career.Balance >= _career.AcademyUpgradeCost))
        {
            var (success, message) = _career.UpgradeYouthAcademy();
            Game.ShowNotice(message);
            if (success) Game.SaveCareer();
        }

        if (area.Height > 210)
        {
            var note = "Facilities improve weekly development. The academy raises the level of next season's intake.";
            Ui.WrappedText(note, new Rectangle(area.X + 16, area.Y + 182, area.Width - 32, area.Height - 190),
                Theme.TextDim, TextSize.Small);
        }
    }

    private void DrawSponsors(Rectangle area)
    {
        Ui.Card(area, new Color(13, 26, 35), Theme.PanelBorder);
        Ui.Text($"SPONSORS  ·  {Theme.Money(Club.AnnualSponsorship)} per season",
            new Vector2(area.X + 16, area.Y + 12), Theme.TextDim, TextSize.Small);

        var y = area.Y + 40;
        var rowHeight = 56;

        foreach (var deal in Club.Sponsors.OrderByDescending(s => s.AnnualValue))
        {
            if (y + rowHeight > area.Bottom - 10) break;

            var row = new Rectangle(area.X + 10, y, area.Width - 20, rowHeight - 6);
            Ui.Fill(row, new Color(18, 33, 43));
            Ui.Fill(new Rectangle(row.X, row.Y, 4, row.Height), deal.Tier == SponsorTier.Main ? Theme.Gold : Theme.Accent);

            Ui.Text(deal.Brand, new Vector2(row.X + 16, row.Y + 8), Theme.Text, TextSize.Small);
            Ui.Text($"{deal.TierLabel}  ·  {deal.SeasonsRemaining} season(s) remaining",
                new Vector2(row.X + 16, row.Y + 28), Theme.TextMuted, TextSize.Small);
            Ui.TextIn(Theme.Money(deal.AnnualValue), new Rectangle(row.Right - 200, row.Y, 180, row.Height),
                Theme.Gold, TextSize.Small, TextAlign.Right);

            y += rowHeight;
        }

        if (Club.Sponsors.Count == 0)
            Ui.Text("No sponsors yet. Accept an offer to bring partners on board.",
                new Vector2(area.X + 16, area.Y + 44), Theme.TextMuted, TextSize.Small);
    }

    private void DrawSponsorOffers(Rectangle area)
    {
        Ui.Card(area, new Color(16, 32, 42), Theme.PanelBorder);
        Ui.Text($"SPONSOR OFFERS ({_career.SponsorOffers.Count})", new Vector2(area.X + 16, area.Y + 12),
            Theme.TextDim, TextSize.Small);

        if (_career.SponsorOffers.Count == 0)
        {
            Ui.Text("No offers on the table. Offers arrive through the season.",
                new Vector2(area.X + 16, area.Y + 42), Theme.TextMuted, TextSize.Small);
            return;
        }

        var y = area.Y + 36;
        foreach (var offer in _career.SponsorOffers.ToList())
        {
            var rowHeight = 68;
            if (y + rowHeight > area.Bottom - 8) break;

            var row = new Rectangle(area.X + 10, y, area.Width - 20, rowHeight - 6);
            Ui.Fill(row, new Color(20, 36, 46));

            Ui.Text($"{offer.Brand}  ·  {offer.TierLabel}", new Vector2(row.X + 14, row.Y + 4), Theme.Text,
                TextSize.Small);
            Ui.Text($"{Theme.Money(offer.AnnualValue)}/season  ·  {offer.Length} seasons  ·  {Theme.Money(offer.BonusPerWin)} per win",
                new Vector2(row.X + 14, row.Y + 22), Theme.TextMuted, TextSize.Small);

            if (Ui.Button(new Rectangle(row.X + 14, row.Y + 40, 96, 22), "Accept", ButtonStyle.Primary))
            {
                var (_, message) = _career.AcceptSponsorOffer(offer.Id);
                Game.ShowNotice(message);
                Game.SaveCareer();
            }

            if (Ui.Button(new Rectangle(row.X + 118, row.Y + 40, 84, 22), "Decline", ButtonStyle.Ghost))
            {
                _career.DeclineSponsorOffer(offer.Id);
                Game.ShowNotice("Offer declined.");
            }

            y += rowHeight;
        }
    }
}
