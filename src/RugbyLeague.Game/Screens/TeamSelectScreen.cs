using Microsoft.Xna.Framework;
using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Model;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game.Screens;

public sealed class TeamSelectScreen : Screen
{
    private static readonly string[] ManagerNames =
    {
        "Alex Morgan", "Sam Whitfield", "Jordan Blake", "Casey Dunne", "Riley Fraser",
        "Morgan Ellis", "Taylor Brookes", "Jamie Colquhoun", "Rowan Price", "Dana Whitlock"
    };

    private readonly Career _preview;
    private readonly List<League> _leagues;
    private readonly Random _random = new();
    private int _leagueIndex;
    private Guid _selectedTeamId;
    private float _scroll;
    private string _managerName;

    public TeamSelectScreen(RugbyGame game) : base(game)
    {
        _random = new Random();
        _managerName = ManagerNames[_random.Next(ManagerNames.Length)];
        _preview = Career.CreateNew(null, _managerName, _random.Next(1, 999_999), 2026);
        _leagues = _preview.Leagues;
        _selectedTeamId = _leagues[0].TeamIds[0];
    }

    private League CurrentLeague => _leagues[_leagueIndex];

    public override void Update(GameTime gameTime)
    {
        if (Input.BackPressed) Game.Screens.Pop();
    }

    public override void Draw()
    {
        DrawCover(Theme.PitchTexture, new Color(28, 40, 40));
        Ui.Fill(View.Bounds, new Color(6, 16, 22, 214));

        Header("Choose your club", "Every club has its own squad, finances, sponsors and board expectations.");

        var tabsY = Page.Y + 112;
        for (var i = 0; i < _leagues.Count; i++)
        {
            var league = _leagues[i];
            var rectangle = new Rectangle(Page.X + i * 232, tabsY, 220, 46);
            if (Ui.Tab(rectangle, $"{league.ShortName} · Tier {league.Tier}", i == _leagueIndex))
            {
                _leagueIndex = i;
                _scroll = 0;
                _selectedTeamId = league.TeamIds.FirstOrDefault();
            }
        }

        DrawClubGrid();
        DrawFooter();
    }

    private void DrawClubGrid()
    {
        var area = new Rectangle(Page.X, Page.Y + 180, Page.Width, Page.Bottom - 130 - (Page.Y + 180));
        var teams = CurrentLeague.TeamIds
            .Select(_preview.TryTeam)
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList();

        var columns = Math.Clamp(area.Width / 360, 2, 4);
        var cardWidth = (area.Width - (columns - 1) * 12) / columns;
        const int cardHeight = 104;
        var rows = (int)Math.Ceiling(teams.Count / (double)columns);
        var contentHeight = rows * (cardHeight + 12);

        _scroll = Ui.HandleScroll(_scroll, area, contentHeight);

        for (var index = 0; index < teams.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var rectangle = new Rectangle(area.X + column * (cardWidth + 12),
                area.Y + row * (cardHeight + 12) - (int)_scroll, cardWidth, cardHeight);

            if (rectangle.Bottom > area.Bottom || rectangle.Y < area.Y) continue;

            var team = teams[index];
            var selected = team.Id == _selectedTeamId;
            if (ClubCard(rectangle, team, selected)) _selectedTeamId = team.Id;
        }

        Ui.ScrollIndicator(area, _scroll, contentHeight);
    }

    private bool ClubCard(Rectangle rectangle, Team team, bool selected)
    {
        var tapped = Ui.Tapped(rectangle);
        var primary = Theme.FromHex(team.PrimaryColor);
        var secondary = Theme.FromHex(team.SecondaryColor);
        var fill = selected ? Color.Lerp(Theme.PanelLight, primary, 0.22f) : Theme.Panel;

        Ui.Card(rectangle, fill, selected ? Theme.Accent : Theme.PanelBorder, shadow: !selected);

        Ui.Fill(new Rectangle(rectangle.X + 10, rectangle.Y + 10, 8, rectangle.Height - 20), primary);
        Ui.Fill(new Rectangle(rectangle.X + 22, rectangle.Y + 10, 3, rectangle.Height - 20), secondary);

        Ui.Text(team.Name, new Vector2(rectangle.X + 34, rectangle.Y + 8), Theme.Text, TextSize.Body);
        Ui.Text(team.Nickname, new Vector2(rectangle.X + 34, rectangle.Y + 44), primary, TextSize.Small);
        Ui.TextIn($"Rep {team.Reputation}", new Rectangle(rectangle.Right - 92, rectangle.Y + 42, 76, 20),
            team.Reputation >= 80 ? Theme.Gold : Theme.TextMuted, TextSize.Small, TextAlign.Right);
        Ui.Text($"{team.StadiumName}  ·  {team.Capacity:N0}", new Vector2(rectangle.X + 34, rectangle.Y + 70),
            Theme.TextMuted, TextSize.Small);

        if (selected)
            Ui.Fill(new Rectangle(rectangle.X + 4, rectangle.Bottom - 4, rectangle.Width - 8, 3), Theme.Accent);

        return tapped;
    }

    private void DrawFooter()
    {
        var footer = new Rectangle(Page.X, Page.Bottom - 118, Page.Width, 112);
        Ui.Card(footer, new Color(14, 28, 38), Theme.PanelBorder);

        var team = _preview.TryTeam(_selectedTeamId);
        var name = team?.Name ?? "No club selected";
        var detail = team is not null
            ? $"{team.Nickname}  ·  {team.StadiumName}  ·  Rating {team.SquadRating}  ·  Rep {team.Reputation}  ·  {team.Squad.Count} players"
            : "Select a club to see its details.";

        Ui.Text(name, new Vector2(footer.X + 20, footer.Y + 10), Theme.Text, TextSize.Subhead);
        Ui.Text(detail, new Vector2(footer.X + 20, footer.Y + 44), Theme.TextMuted, TextSize.Small);

        if (Ui.Button(new Rectangle(footer.X + 20, footer.Y + 76, 96, 30), "Back", ButtonStyle.Ghost))
            Game.Screens.Pop();

        if (Ui.Button(new Rectangle(footer.Right - 184, footer.Y + 32, 164, 48), "Take charge",
                ButtonStyle.Primary, team is not null))
        {
            StartCareer();
        }

        var managerBox = new Rectangle(footer.Right - 184 - 16 - 56 - 8 - 216, footer.Y + 30, 216, 52);
        Ui.Fill(managerBox, Theme.BackgroundDeep);
        Ui.Border(managerBox, Theme.PanelBorder);
        Ui.Text("MANAGER", new Vector2(managerBox.X + 10, managerBox.Y + 2), Theme.TextDim, TextSize.Small);
        Ui.Text(_managerName, new Vector2(managerBox.X + 10, managerBox.Y + 28), Theme.Text, TextSize.Small);

        if (Ui.Button(new Rectangle(managerBox.Right + 8, managerBox.Y + 8, 56, 36), "New", ButtonStyle.Ghost))
        {
            _managerName = ManagerNames[_random.Next(ManagerNames.Length)];
            _preview.ManagerName = _managerName;
        }
    }

    private void StartCareer()
    {
        _preview.TakeCharge(_selectedTeamId, _managerName);
        Game.StartCareer(_preview);
    }
}
