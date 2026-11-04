using Microsoft.Xna.Framework;
using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Model;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public enum PlayerScreenMode
{
    Squad,
    FreeAgent,
    Listing
}

public sealed class PlayerScreen : Screen
{
    private readonly Player _player;
    private readonly PlayerScreenMode _mode;
    private readonly Guid _clubId;
    private int _fee;
    private int _wage;
    private int _years = 2;
    private int _askPrice;

    public PlayerScreen(RugbyGame game, Player player, PlayerScreenMode mode, Guid clubId = default) : base(game)
    {
        _player = player;
        _mode = mode;
        _clubId = clubId;
        _fee = PlayerValue.Calculate(player);
        _wage = PlayerValue.WeeklyWageFor(player);
        _askPrice = PlayerValue.Calculate(player);
    }

    private Career? Career => Game.Career;

    public override void Update(GameTime gameTime)
    {
        if (Input.BackPressed) Game.Screens.Pop();
    }

    public override void Draw()
    {
        DrawCover(Theme.PitchTexture, new Color(24, 36, 36));
        Ui.Fill(View.Bounds, new Color(7, 17, 23, 232));

        var area = Page;
        if (Ui.Button(new Rectangle(area.X, area.Y, 120, 40), "Back", ButtonStyle.Ghost))
        {
            Game.Screens.Pop();
            return;
        }

        var card = new Rectangle(area.X, area.Y + 54, area.Width, area.Height - 54);
        Ui.Card(card, new Color(14, 28, 38), Theme.PanelBorder);

        DrawIdentity(card);
        DrawAttributes(new Rectangle(card.X + 20, card.Y + 130, (int)(card.Width * 0.44f), card.Height - 150));
        DrawActionPanel(new Rectangle(card.X + (int)(card.Width * 0.48f), card.Y + 130,
            card.Width - (int)(card.Width * 0.48f) - 20, card.Height - 150));
    }

    private void DrawIdentity(Rectangle card)
    {
        var primary = Theme.FromHex(Career?.TryTeam(_clubId)?.PrimaryColor ?? "#2E9E6B");
        var badge = new Rectangle(card.X + 20, card.Y + 20, 72, 72);
        Ui.Fill(badge, primary);
        Ui.TextIn(_player.ShirtNumber.ToString(), badge, Theme.ContrastingText(primary), TextSize.Heading,
            TextAlign.Center);

        Ui.Text(_player.Name, new Vector2(badge.Right + 16, card.Y + 22), Theme.Text, TextSize.Heading);
        Ui.Text($"{PositionName(_player.Position)}  ·  {_player.Age} years  ·  {_player.Rating} overall  ·  potential {_player.Potential}",
            new Vector2(badge.Right + 16, card.Y + 64), Theme.TextMuted, TextSize.Small);

        var clubName = Career?.TryTeam(_clubId)?.Name ?? Career?.PlayerTeam.Name ?? "Free agent";
        Ui.Text(clubName, new Vector2(badge.Right + 16, card.Y + 88), Theme.TextDim, TextSize.Small);

        var wage = Theme.Money(_player.Contract.WeeklyWage) + "/wk";
        Ui.TextIn(wage, new Rectangle(card.Right - 260, card.Y + 30, 236, 24), Theme.TextMuted, TextSize.Small,
            TextAlign.Right);
        Ui.TextIn($"Contract {_player.Contract.YearsRemaining} season(s)", new Rectangle(card.Right - 260, card.Y + 54,
            236, 24), Theme.TextDim, TextSize.Small, TextAlign.Right);
        Ui.TextIn($"Value {Theme.Money(PlayerValue.Calculate(_player))}", new Rectangle(card.Right - 260, card.Y + 78,
            236, 24), Theme.Gold, TextSize.Small, TextAlign.Right);

        Ui.Fill(new Rectangle(card.X + 20, card.Y + 112, card.Width - 40, 1), Theme.PanelBorder);
    }

    private void DrawAttributes(Rectangle area)
    {
        Ui.Text("ATTRIBUTES", new Vector2(area.X, area.Y), Theme.TextDim, TextSize.Small);
        var y = area.Y + 30;

        foreach (var (name, value) in _player.Attributes.Snapshot())
        {
            Ui.Text(name, new Vector2(area.X, y), Theme.TextMuted, TextSize.Small);
            Ui.TextIn(value.ToString(), new Rectangle(area.X + area.Width - 60, y - 4, 40, 22), Theme.Text,
                TextSize.Small, TextAlign.Right);
            Ui.ProgressBar(new Rectangle(area.X + 100, y + 5, area.Width - 168, 7), value / 99f,
                value >= 80 ? Theme.Accent : value >= 60 ? Theme.Gold : Theme.Danger);
            y += 27;
        }

        y += 10;
        Ui.Text("SEASON", new Vector2(area.X, y), Theme.TextDim, TextSize.Small);
        y += 28;
        var stats = _player.Season;
        Ui.Text($"{stats.Appearances} apps  ·  {stats.Tries} tries  ·  {stats.Goals} goals  ·  {stats.Metres} m",
            new Vector2(area.X, y), Theme.TextMuted, TextSize.Small);
        y += 24;
        Ui.Text($"{stats.Tackles} tackles  ·  {stats.Points} points  ·  {stats.ManOfTheMatch} MOTM",
            new Vector2(area.X, y), Theme.TextMuted, TextSize.Small);
        y += 28;

        Ui.Text($"Form {_player.Form}  ·  Fitness {_player.Fitness}%  ·  Sharpness {_player.Sharpness}%  ·  Morale {_player.Morale}%",
            new Vector2(area.X, y), Theme.TextDim, TextSize.Small);

        if (_player.IsInjured)
            Ui.Text($"Injured: {_player.Injury!.Type} — {_player.Injury.WeeksRemaining} week(s) out",
                new Vector2(area.X, y + 24), Theme.Loss, TextSize.Small);
    }

    private void DrawActionPanel(Rectangle area)
    {
        Ui.Card(area, new Color(18, 34, 44), Theme.PanelBorder);
        var career = Career;
        if (career is null)
        {
            Ui.Text("No career loaded.", new Vector2(area.X + 16, area.Y + 16), Theme.TextMuted, TextSize.Small);
            return;
        }

        var y = area.Y + 18;
        switch (_mode)
        {
            case PlayerScreenMode.Squad:
                DrawSquadActions(area, career, ref y);
                break;
            case PlayerScreenMode.FreeAgent:
                DrawFreeAgentActions(area, career, ref y);
                break;
            case PlayerScreenMode.Listing:
                DrawListingActions(area, career, ref y);
                break;
        }
    }

    private void DrawSquadActions(Rectangle area, Career career, ref int y)
    {
        var listing = career.Transfers.ListingFor(_player.Id);
        Ui.Text("TRANSFER STATUS", new Vector2(area.X + 16, y), Theme.TextDim, TextSize.Small);
        y += 30;

        if (listing is not null)
        {
            Ui.Text($"Listed at {Theme.Money(listing.AskingPrice)}", new Vector2(area.X + 16, y), Theme.Gold,
                TextSize.Small);
            y += 28;

            Stepper(area, ref y, "Asking price", ref _askPrice, 50_000, 15_000, 4_000_000, Theme.ShortMoney);

            if (Ui.Button(new Rectangle(area.X + 16, y, area.Width - 32, 42), "Update asking price",
                    ButtonStyle.Primary))
            {
                var (_, message) = career.ListPlayer(_player.Id, _askPrice);
                Game.ShowNotice(message);
            }

            y += 52;

            if (Ui.Button(new Rectangle(area.X + 16, y, area.Width - 32, 40), "Remove from transfer list",
                    ButtonStyle.Ghost))
            {
                career.UnlistPlayer(_player.Id);
                Game.ShowNotice($"{_player.Name} removed from the transfer list.");
            }

            y += 52;
        }
        else
        {
            Ui.Text("Not listed. Interested clubs can only bid for listed players.", new Vector2(area.X + 16, y),
                Theme.TextMuted, TextSize.Small);
            y += 30;

            Stepper(area, ref y, "Asking price", ref _askPrice, 50_000, 15_000, 4_000_000, Theme.ShortMoney);

            if (Ui.Button(new Rectangle(area.X + 16, y, area.Width - 32, 42), "List for transfer",
                    ButtonStyle.Primary, career.PlayerTeam.Squad.Count > 21))
            {
                var (_, message) = career.ListPlayer(_player.Id, _askPrice);
                Game.ShowNotice(message);
            }

            y += 52;
        }

        var offers = career.Transfers.PendingOffers.Where(o => o.PlayerId == _player.Id).ToList();
        if (offers.Count > 0)
        {
            Ui.Divider(new Rectangle(area.X + 16, y, area.Width - 32, 1));
            y += 14;
            Ui.Text($"OFFERS ({offers.Count})", new Vector2(area.X + 16, y), Theme.TextDim, TextSize.Small);
            y += 26;

            foreach (var offer in offers.Take(3))
            {
                Ui.Text($"{offer.ToClubName}  ·  {Theme.Money(offer.Fee)}", new Vector2(area.X + 16, y), Theme.Text,
                    TextSize.Small);

                if (Ui.Button(new Rectangle(area.Right - 190, y - 6, 78, 30), "Accept", ButtonStyle.Primary))
                {
                    var (success, message) = career.RespondToOffer(offer.Id, true);
                    Game.ShowNotice(message);
                    if (success)
                    {
                        Game.SaveCareer();
                        Game.Screens.Pop();
                        return;
                    }
                }

                if (Ui.Button(new Rectangle(area.Right - 104, y - 6, 84, 30), "Reject", ButtonStyle.Ghost))
                    Game.ShowNotice(career.RespondToOffer(offer.Id, false).Message);

                y += 38;
            }
        }
    }

    private void DrawFreeAgentActions(Rectangle area, Career career, ref int y)
    {
        Ui.Text("OFFER A CONTRACT", new Vector2(area.X + 16, y), Theme.TextDim, TextSize.Small);
        y += 30;

        Stepper(area, ref y, "Weekly wage", ref _wage, 100, 1_100, 30_000, v => Theme.Money(v));
        Stepper(area, ref y, "Contract length", ref _years, 1, 1, 4, v => $"{v} season(s)");

        var capImpact = (long)_wage * 52;
        Ui.Text($"Salary cap impact {Theme.Money(capImpact)}  ·  space {Theme.Money(career.CapSpace)}",
            new Vector2(area.X + 16, y), capImpact <= career.CapSpace ? Theme.TextMuted : Theme.Loss, TextSize.Small);
        y += 34;

        if (Ui.Button(new Rectangle(area.X + 16, y, area.Width - 32, 44), "Sign free agent", ButtonStyle.Primary))
        {
            var (success, message) = career.SignFreeAgent(_player.Id, _wage, _years);
            Game.ShowNotice(message);
            if (success)
            {
                Game.SaveCareer();
                Game.Screens.Pop();
            }
        }

        y += 56;
        Ui.Text($"Agent expects about {Theme.Money(PlayerValue.WeeklyWageFor(_player))} per week.",
            new Vector2(area.X + 16, y), Theme.TextDim, TextSize.Small);
    }

    private void DrawListingActions(Rectangle area, Career career, ref int y)
    {
        var listing = career.Transfers.ListingFor(_player.Id);
        if (listing is null)
        {
            Ui.Text("This player is no longer listed.", new Vector2(area.X + 16, y), Theme.TextMuted, TextSize.Small);
            return;
        }

        Ui.Text("MAKE A TRANSFER OFFER", new Vector2(area.X + 16, y), Theme.TextDim, TextSize.Small);
        y += 30;
        Ui.Text($"{career.TeamById(listing.ClubId).Name} want {Theme.Money(listing.AskingPrice)}",
            new Vector2(area.X + 16, y), Theme.Gold, TextSize.Small);
        y += 28;

        Stepper(area, ref y, "Transfer fee", ref _fee, 25_000, 15_000, 6_000_000, Theme.ShortMoney);
        Stepper(area, ref y, "Weekly wage", ref _wage, 100, 1_100, 30_000, v => Theme.Money(v));
        Stepper(area, ref y, "Contract length", ref _years, 1, 1, 4, v => $"{v} season(s)");

        Ui.Text($"Budget {Theme.Money(career.PlayerTeam.Finances.TransferBudget)}  ·  cap space {Theme.Money(career.CapSpace)}",
            new Vector2(area.X + 16, y + 2), Theme.TextMuted, TextSize.Small);
        y += 34;

        if (Ui.Button(new Rectangle(area.X + 16, y, area.Width - 32, 44), "Submit offer", ButtonStyle.Primary))
        {
            var (success, message) = career.MakeTransferOffer(_player.Id, _fee, _wage, _years);
            Game.ShowNotice(message, 5f);
            if (success)
            {
                Game.SaveCareer();
                Game.Screens.Pop();
            }
        }
    }

    private void Stepper(Rectangle area, ref int y, string label, ref int value, int step, int minimum, int maximum,
        Func<int, string> format)
    {
        var row = new Rectangle(area.X + 16, y, area.Width - 32, 42);
        Ui.Text(label.ToUpperInvariant(), new Vector2(row.X, row.Y + 2), Theme.TextDim, TextSize.Small);
        Ui.TextIn(format(value), new Rectangle(row.X, row.Y + 18, row.Width - 96, 22), Theme.Text, TextSize.Small);

        if (Ui.Button(new Rectangle(row.Right - 88, row.Y + 4, 40, 32), "-", ButtonStyle.Normal))
            value = Math.Max(minimum, value - step);

        if (Ui.Button(new Rectangle(row.Right - 42, row.Y + 4, 40, 32), "+", ButtonStyle.Normal))
            value = Math.Min(maximum, value + step);

        y += 46;
    }

    private static string PositionName(PlayerPosition position) => position switch
    {
        PlayerPosition.Fullback => "Fullback",
        PlayerPosition.Wing => "Wing",
        PlayerPosition.Centre => "Centre",
        PlayerPosition.FiveEighth => "Five-eighth",
        PlayerPosition.Halfback => "Halfback",
        PlayerPosition.Prop => "Prop",
        PlayerPosition.Hooker => "Hooker",
        PlayerPosition.SecondRow => "Second row",
        PlayerPosition.Lock => "Lock",
        _ => position.ToString()
    };
}
