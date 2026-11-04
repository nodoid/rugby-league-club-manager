using Microsoft.Xna.Framework;
using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Model;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public sealed partial class CareerHubScreen
{
    private enum TransferView
    {
        Market,
        FreeAgents,
        Offers,
        Listed
    }

    private TransferView _transferView = TransferView.Market;
    private float _transferScroll;

    private void DrawTransfers(Rectangle area)
    {
        var header = new Rectangle(area.X, area.Y, area.Width, 48);
        Ui.Text("Transfers", new Vector2(header.X, header.Y + 2), Theme.Text, TextSize.Subhead);

        var windowOpen = _career.Round < _career.Transfers.WindowClosesRound && _career.Phase == CompetitionPhase.Regular;
        Ui.TextIn(windowOpen
                ? $"Window closes after round {_career.Transfers.WindowClosesRound}  ·  budget {Theme.Money(_career.PlayerTeam.Finances.TransferBudget)}"
                : "The transfer window is closed; deals resume next season.",
            new Rectangle(header.X + 180, header.Y, header.Width - 180, 40),
            windowOpen ? Theme.TextMuted : Theme.Gold, TextSize.Small, TextAlign.Left);

        var views = new[] { "Transfer list", "Free agents", $"Offers ({_career.Transfers.PendingOffers.Count(o => o.ToClubId == _career.PlayerTeamId)})", "My listings" };
        var x = area.X;
        foreach (var view in Enum.GetValues<TransferView>())
        {
            if (Ui.Tab(new Rectangle(x, area.Y + 52, 176, 38), views[(int)view], _transferView == view))
            {
                _transferView = view;
                _transferScroll = 0;
            }

            x += 184;
        }

        var list = new Rectangle(area.X, area.Y + 100, area.Width, area.Height - 100);
        Ui.Card(list, new Color(13, 26, 35), Theme.PanelBorder);

        switch (_transferView)
        {
            case TransferView.Market: DrawMarket(list); break;
            case TransferView.FreeAgents: DrawFreeAgents(list); break;
            case TransferView.Offers: DrawOffers(list); break;
            case TransferView.Listed: DrawMyListings(list); break;
        }
    }

    private void DrawMarket(Rectangle list)
    {
        var listings = _career.Transfers.Listings
            .Where(l => l.ClubId != _career.PlayerTeamId)
            .OrderBy(l => l.AskingPrice)
            .ToList();

        var contentHeight = listings.Count * 58 + 8;
        _transferScroll = Ui.HandleScroll(_transferScroll, list, contentHeight);

        var y = list.Y + 6 - (int)_transferScroll;
        foreach (var listing in listings)
        {
            var seller = _career.TryTeam(listing.ClubId);
            var player = seller?.Squad.FirstOrDefault(p => p.Id == listing.PlayerId);
            if (player is null) continue;

            if (y + 58 > list.Y && y < list.Bottom)
            {
                var row = new Rectangle(list.X + 8, y, list.Width - 24, 54);
                Ui.Fill(row, new Color(17, 31, 41));

                Ui.Fill(new Rectangle(row.X + 8, row.Y + 10, 8, 34),
                    Theme.FromHex(seller?.PrimaryColor ?? "#2E9E6B"));
                Ui.Text(player.Name, new Vector2(row.X + 26, row.Y + 8), Theme.Text, TextSize.Small);
                Ui.Text($"{PositionLabel(player.Position)}  ·  {player.Age}  ·  {player.Rating} OVR  ·  {seller?.Name}",
                    new Vector2(row.X + 26, row.Y + 28), Theme.TextMuted, TextSize.Small);

                Ui.TextIn(Theme.Money(listing.AskingPrice),
                    new Rectangle(row.Right - 300, row.Y, 140, row.Height), Theme.Gold, TextSize.Small,
                    TextAlign.Right);
                Ui.TextIn($"{Theme.ShortMoney(PlayerValue.WeeklyWageFor(player))}/wk",
                    new Rectangle(row.Right - 160, row.Y, 90, row.Height), Theme.TextDim, TextSize.Small,
                    TextAlign.Right);

                if (Ui.Button(new Rectangle(row.Right - 148, row.Y + 8, 76, 36), "View", ButtonStyle.Ghost))
                    Game.Screens.Push(new PlayerScreen(Game, player, PlayerScreenMode.Listing, listing.ClubId));

                if (Ui.Button(new Rectangle(row.Right - 66, row.Y + 8, 58, 36), "Bid", ButtonStyle.Primary))
                    Game.Screens.Push(new PlayerScreen(Game, player, PlayerScreenMode.Listing, listing.ClubId));
            }

            y += 58;
        }

        if (listings.Count == 0)
            Ui.Text("No players are listed right now. Check back after the next round.",
                new Vector2(list.X + 16, list.Y + 16), Theme.TextMuted, TextSize.Small);

        Ui.ScrollIndicator(list, _transferScroll, contentHeight);
    }

    private void DrawFreeAgents(Rectangle list)
    {
        var agents = _career.Transfers.FreeAgents;
        var contentHeight = agents.Count * 58 + 8;
        _transferScroll = Ui.HandleScroll(_transferScroll, list, contentHeight);

        var y = list.Y + 6 - (int)_transferScroll;
        foreach (var player in agents)
        {
            if (y + 58 > list.Y && y < list.Bottom)
            {
                var row = new Rectangle(list.X + 8, y, list.Width - 24, 54);
                Ui.Fill(row, new Color(17, 31, 41));
                Ui.Text(player.Name, new Vector2(row.X + 20, row.Y + 8), Theme.Text, TextSize.Small);
                Ui.Text($"{PositionLabel(player.Position)}  ·  {player.Age}  ·  {player.Rating} OVR  ·  potential {player.Potential}",
                    new Vector2(row.X + 20, row.Y + 28), Theme.TextMuted, TextSize.Small);

                Ui.TextIn(Theme.Money(PlayerValue.WeeklyWageFor(player)) + "/wk",
                    new Rectangle(row.Right - 320, row.Y, 150, row.Height), Theme.TextDim, TextSize.Small,
                    TextAlign.Right);

                if (Ui.Button(new Rectangle(row.Right - 156, row.Y + 8, 148, 36), "Offer contract",
                        ButtonStyle.Primary))
                    Game.Screens.Push(new PlayerScreen(Game, player, PlayerScreenMode.FreeAgent));
            }

            y += 58;
        }

        if (agents.Count == 0)
            Ui.Text("No free agents available.", new Vector2(list.X + 16, list.Y + 16), Theme.TextMuted, TextSize.Small);

        Ui.ScrollIndicator(list, _transferScroll, contentHeight);
    }

    private void DrawOffers(Rectangle list)
    {
        var offers = _career.Transfers.Offers
            .Where(o => o.ToClubId == _career.PlayerTeamId)
            .OrderByDescending(o => o.Fee)
            .ToList();

        var contentHeight = offers.Count * 64 + 8;
        _transferScroll = Ui.HandleScroll(_transferScroll, list, contentHeight);
        var y = list.Y + 6 - (int)_transferScroll;

        foreach (var offer in offers)
        {
            if (y + 64 > list.Y && y < list.Bottom)
            {
                var row = new Rectangle(list.X + 8, y, list.Width - 24, 60);
                var pending = offer.Status == TransferStatus.Pending;
                Ui.Fill(row, pending ? new Color(17, 31, 41) : new Color(13, 24, 31));

                Ui.Text(offer.PlayerName, new Vector2(row.X + 20, row.Y + 8), Theme.Text, TextSize.Small);
                Ui.Text($"{offer.ToClubName}  ·  {offer.Status}  ·  round {offer.Round}",
                    new Vector2(row.X + 20, row.Y + 28), Theme.TextMuted, TextSize.Small);
                Ui.TextIn(Theme.Money(offer.Fee) + " offered", new Rectangle(row.Right - 380, row.Y, 170, row.Height),
                    Theme.Gold, TextSize.Small, TextAlign.Right);

                if (pending)
                {
                    if (Ui.Button(new Rectangle(row.Right - 188, row.Y + 12, 88, 36), "Accept", ButtonStyle.Primary))
                    {
                        var (_, message) = _career.RespondToOffer(offer.Id, true);
                        Game.ShowNotice(message);
                        Game.SaveCareer();
                    }

                    if (Ui.Button(new Rectangle(row.Right - 92, row.Y + 12, 80, 36), "Reject", ButtonStyle.Ghost))
                    {
                        var (_, message) = _career.RespondToOffer(offer.Id, false);
                        Game.ShowNotice(message);
                    }
                }
                else
                {
                    Ui.TextIn(offer.Status.ToString(), new Rectangle(row.Right - 120, row.Y, 100, row.Height),
                        Theme.TextDim, TextSize.Small, TextAlign.Right);
                }
            }

            y += 64;
        }

        if (offers.Count == 0)
            Ui.Text("No offers. List a player to attract bids from rival clubs.",
                new Vector2(list.X + 16, list.Y + 16), Theme.TextMuted, TextSize.Small);

        Ui.ScrollIndicator(list, _transferScroll, contentHeight);
    }

    private void DrawMyListings(Rectangle list)
    {
        var listings = _career.Transfers.Listings.Where(l => l.ClubId == _career.PlayerTeamId).ToList();
        var contentHeight = listings.Count * 58 + 8;
        _transferScroll = Ui.HandleScroll(_transferScroll, list, contentHeight);
        var y = list.Y + 6 - (int)_transferScroll;

        foreach (var listing in listings)
        {
            var player = Club.Squad.FirstOrDefault(p => p.Id == listing.PlayerId);
            if (player is null) continue;

            if (y + 58 > list.Y && y < list.Bottom)
            {
                var row = new Rectangle(list.X + 8, y, list.Width - 24, 54);
                Ui.Fill(row, new Color(17, 31, 41));
                Ui.Text(player.Name, new Vector2(row.X + 20, row.Y + 8), Theme.Text, TextSize.Small);
                Ui.Text($"{PositionLabel(player.Position)}  ·  {player.Age}  ·  {player.Rating} OVR  ·  {Theme.ShortMoney(player.Contract.WeeklyWage)}/wk",
                    new Vector2(row.X + 20, row.Y + 28), Theme.TextMuted, TextSize.Small);

                Ui.TextIn(Theme.Money(listing.AskingPrice), new Rectangle(row.Right - 300, row.Y, 120, row.Height),
                    Theme.Gold, TextSize.Small, TextAlign.Right);

                if (Ui.Button(new Rectangle(row.Right - 172, row.Y + 9, 156, 36), "Edit listing",
                        ButtonStyle.Ghost))
                    Game.Screens.Push(new PlayerScreen(Game, player, PlayerScreenMode.Squad));

                if (Ui.Button(new Rectangle(row.Right - 74, row.Y + 12, 60, 30), "Delist", ButtonStyle.Normal))
                {
                    _career.UnlistPlayer(player.Id);
                    Game.ShowNotice($"{player.Name} removed from the transfer list.");
                }
            }

            y += 58;
        }

        if (listings.Count == 0)
            Ui.Text("You have nobody listed. Open a player from the squad screen to list them.",
                new Vector2(list.X + 16, list.Y + 16), Theme.TextMuted, TextSize.Small);

        Ui.ScrollIndicator(list, _transferScroll, contentHeight);
    }
}
