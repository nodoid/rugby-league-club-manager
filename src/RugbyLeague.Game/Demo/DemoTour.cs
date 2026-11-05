using Microsoft.Xna.Framework;
using RugbyLeague.Core.Careers;
using RugbyLeague.Game.Screens;

namespace RugbyLeague.Game.Demo;

/// <summary>
/// Plays a scripted tour of the game. Store screenshots and previews are captured on simulators
/// that nobody is tapping, so the tour pushes touches through the same input path a finger uses.
/// Enabled by the <c>RL_DEMO</c> environment variable or the <c>--demo</c> argument.
/// </summary>
public sealed class DemoTour
{
    private readonly RugbyGame _game;
    private readonly Queue<(float At, Action Run)> _steps = new();
    private readonly List<(float At, int Id)> _releases = new();
    private readonly Dictionary<int, Vector2> _held = new();

    private float _time;
    private int _nextTouch = 40;
    private int _stick;

    public DemoTour(RugbyGame game)
    {
        _game = game;
        Build();
    }

    public void Update(GameTime gameTime)
    {
        _time += (float)gameTime.ElapsedGameTime.TotalSeconds;

        for (var index = _releases.Count - 1; index >= 0; index--)
        {
            if (_releases[index].At > _time) continue;

            var id = _releases[index].Id;
            if (_held.TryGetValue(id, out var position))
            {
                _game.Input.InjectTouch(id, position, false);
                _held.Remove(id);
            }

            _releases.RemoveAt(index);
        }

        while (_steps.Count > 0 && _steps.Peek().At <= _time)
            _steps.Dequeue().Run();
    }

    private void At(float seconds, Action action) => _steps.Enqueue((seconds, action));

    private void Tap(float x, float y, float hold = 0.1f)
    {
        var id = _nextTouch++;
        var position = new Vector2(x, y) * _game.View.Scale;
        _held[id] = position;
        _game.Input.InjectTouch(id, position, true);
        _releases.Add((_time + hold, id));
    }

    private int Hold(float x, float y)
    {
        var id = _nextTouch++;
        var position = new Vector2(x, y) * _game.View.Scale;
        _held[id] = position;
        _game.Input.InjectTouch(id, position, true);
        return id;
    }

    private void Release(int id)
    {
        if (!_held.TryGetValue(id, out var position)) return;

        _game.Input.InjectTouch(id, position, false);
        _held.Remove(id);
    }

    private void Build()
    {
        var width = _game.View.Width;
        var height = _game.View.Height;
        var page = _game.View.Page();

        // Career hub navigation column: the first tab starts ten units into the nav card.
        var navX = page.X + 100;
        var tabY = page.Y + 96f + 31f;
        var matchdayY = page.Y + page.Height - 116f + 24f;

        // Matchday screen: the primary button sits in the bottom row of the team-sheet card.
        var playX = page.X + 145f;
        var playY = height - 28f - 78f + 28f;

        // 3D match: stick on the left, PASS/KICK/TACKLE stacked on the right.
        var stickX = 160f;
        var stickY = height - 130f;
        var passX = width - 258f;
        var passY = height - 158f;
        var kickX = width - 148f;
        var kickY = height - 198f;

        At(0.8f, () => _game.Screens.ReplaceAll(new MainMenuScreen(_game)));

        // Each stop holds for about four seconds so captures always land on a settled screen.
        At(5.0f, () =>
        {
            var career = Career.CreateNew(null, "Alex Morgan", 4242, 2026);
            career.TakeCharge(career.Leagues[0].TeamIds[0], "Alex Morgan");
            _game.StartCareer(career);
        });

        At(9.0f, () => Tap(navX, tabY + 46f));            // Squad
        At(13.0f, () => Tap(navX, tabY + 46f * 6f));      // Competitions
        At(17.0f, () => Tap(navX, tabY));                 // Dashboard
        At(17.8f, () => Tap(navX, matchdayY));            // Matchday

        At(21.5f, () => Tap(playX, playY));               // Play 3D match
        At(23.4f, () => Tap(width / 2f, height * 0.5f));  // dismiss the how-to-play card
        At(23.8f, () => _stick = Hold(stickX, stickY - 62f));
        At(26.6f, () => Release(_stick));
        At(27.0f, () => Tap(passX, passY));
        At(28.6f, () => Tap(kickX, kickY));
        At(30.5f, () => _game.Exit());
    }
}
