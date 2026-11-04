using Microsoft.Xna.Framework;
using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Model;

namespace RugbyLeague.Game.Screens;

public sealed partial class ThreeDMatchScreen
{
    private sealed class Actor
    {
        public required Player Player { get; init; }
        public required bool IsHome { get; init; }
        public required int Shirt { get; init; }
        public Vector3 Position;
        public float TackleCooldown;
        public bool IsControlled;
    }

    private const float PitchLength = 100f;
    private const float PitchWidth = 68f;

    private readonly List<Actor> _actors = new();
    private readonly Dictionary<Guid, PlayerMatchStats> _playerStats = new();
    private readonly List<MatchEvent> _events = new();
    private readonly List<MatchInjury> _injuries = new();
    private readonly MatchTeamStats _homeStats = new();
    private readonly MatchTeamStats _awayStats = new();
    private readonly Random _rng = new(4242);

    private bool _initialised;
    private bool _finished;
    private bool _submitted;
    private MatchResult? _result;

    private bool _homePossession;
    private int _homeDirection = 1;
    private int _half = 1;
    private bool _halfTimeBreak;
    private float _breakTimer;
    private float _clock;
    private int _tackle;
    private int _homeScore;
    private int _awayScore;
    private int _homeTries;
    private int _awayTries;
    private bool _goldenPoint;
    private float _pause;
    private float _messageTime;
    private string _message = "";
    private float _lastDt;
    private float _homePossessionTime;
    private float _awayPossessionTime;
    private float _carryStartX;
    private float _sprintHeld;

    private Actor? _carrier;
    private Actor? _controlled;
    private Vector3 _ball = new(50, 0.4f, 34);

    private bool _flight;
    private bool _flightIsKick;
    private Vector3 _flightFrom;
    private Vector3 _flightTo;
    private float _flightElapsed;
    private float _flightDuration;
    private Actor? _flightTarget;

    // on-screen controls (virtual coordinates)
    private Rectangle _stickArea;
    private Rectangle _passButton;
    private Rectangle _kickButton;
    private Rectangle _tackleButton;
    private Rectangle _sprintButton;
    private int _stickId = -1;
    private Vector2 _stickCentre;
    private Vector2 _stickValue;
    private float _stickRadius = 82f;

    private string TeamName(bool home) => home ? _setup.Home.TeamName : _setup.Away.TeamName;
    private Guid TeamId(bool home) => home ? _setup.Home.TeamId : _setup.Away.TeamId;
    private TeamSheet Sheet(bool home) => home ? _setup.Home : _setup.Away;

    private int AttackDirection(bool home) => home ? _homeDirection : -_homeDirection;

    private float TryLine(bool home) => 50f + 40f * AttackDirection(home);

    private void Initialise()
    {
        if (_initialised) return;
        _initialised = true;

        _playerIsHome = _career.PlayerTeamId == _setup.Home.TeamId;

        AddActors(_setup.Home, true);
        AddActors(_setup.Away, false);

        foreach (var actor in _actors)
            _playerStats[actor.Player.Id] = new PlayerMatchStats
            {
                PlayerId = actor.Player.Id,
                TeamId = actor.IsHome ? TeamId(true) : TeamId(false),
                Name = actor.Player.Name
            };

        LayoutControls();

        _homePossession = _rng.Next(2) == 0;
        ResetPositions();
        _carrier = Nearest(_homePossession, _ball);
        _controlled = _carrier;
        AddEvent(MatchEventKind.Commentary, $"{TeamName(_homePossession)} get the match under way.", TeamId(_homePossession));
        ShowMessage($"Kick-off — {TeamName(_homePossession)} ball", 2.4f);
    }

    private void AddActors(TeamSheet sheet, bool home)
    {
        foreach (var player in sheet.Starters)
        {
            _actors.Add(new Actor
            {
                Player = player,
                IsHome = home,
                Shirt = player.ShirtNumber,
                Position = new Vector3(50, 0, 34)
            });
        }
    }

    private void LayoutControls()
    {
        var width = View.Width;
        var height = View.Height;
        _layoutWidth = width;
        _stickArea = new Rectangle(60, (int)height - 230, 200, 200);
        _stickCentre = new Vector2(_stickArea.Center.X, _stickArea.Center.Y);
        _stickRadius = 78f;

        _passButton = new Rectangle((int)width - 310, (int)height - 210, 104, 104);
        _kickButton = new Rectangle((int)width - 200, (int)height - 250, 104, 104);
        _tackleButton = new Rectangle((int)width - 200, (int)height - 136, 104, 104);
        _sprintButton = new Rectangle((int)width - 424, (int)height - 150, 88, 88);
    }

    private void ShowMessage(string message, float seconds)
    {
        _message = message;
        _messageTime = seconds;
    }

    private void UpdateStick()
    {
        if (_stickId < 0)
        {
            foreach (var pointer in Input.Pointers)
            {
                if (pointer.Pressed && _stickArea.Contains(pointer.Position.ToPoint()))
                {
                    _stickId = pointer.Id;
                    break;
                }
            }
        }

        var active = false;
        if (_stickId >= 0)
        {
            foreach (var pointer in Input.Pointers)
            {
                if (pointer.Id != _stickId || !pointer.IsDown) continue;
                active = true;
                var vector = (pointer.Position - _stickCentre) / _stickRadius;
                if (vector.LengthSquared() > 1) vector.Normalize();
                _stickValue = vector;
            }
        }

        if (!active)
        {
            _stickId = -1;
            _stickValue = Vector2.Zero;
        }
    }

    private void Simulate(float dt)
    {
        _lastDt = dt;

        if (_finished)
        {
            UpdateCamera(dt);
            return;
        }

        if (_messageTime > 0) _messageTime -= dt;

        UpdateStick();

        if (_pause > 0)
        {
            _pause -= dt;
            UpdateCamera(dt);
            return;
        }

        if (_halfTimeBreak)
        {
            _breakTimer -= dt;
            if (_breakTimer <= 0)
            {
                _halfTimeBreak = false;
                _half = 2;
                _homeDirection = -1;
                Kickoff(!_homePossession);
            }

            UpdateCamera(dt);
            return;
        }

        _clock += dt * 0.9f;

        if (_homePossession) _homePossessionTime += dt;
        else _awayPossessionTime += dt;

        if (_half == 1 && _clock >= 40f)
        {
            _halfTimeBreak = true;
            _breakTimer = 2.2f;
            ShowMessage("Half time", 2.2f);
            AddEvent(MatchEventKind.Commentary, $"Half time: {TeamName(true)} {_homeScore}-{_awayScore} {TeamName(false)}.",
                null);
            UpdateCamera(dt);
            return;
        }

        if (!_goldenPoint && _clock >= 80f)
        {
            if (_homeScore == _awayScore && _setup.MustHaveWinner)
            {
                _goldenPoint = true;
                ShowMessage("Golden point — next score wins", 3f);
                AddEvent(MatchEventKind.Commentary, "Extra time. Golden point decides it.", null);
            }
            else
            {
                FinishMatch();
                UpdateCamera(dt);
                return;
            }
        }

        if (_goldenPoint && _clock >= 90f)
        {
            if (_homeScore == _awayScore)
            {
                var homeKicker = BestKicker(true);
                var awayKicker = BestKicker(false);
                var homeWins = homeKicker.Player.Attributes.Kicking + _rng.Next(20) >=
                               awayKicker.Player.Attributes.Kicking + _rng.Next(20);

                if (homeWins) _homeScore += 1;
                else _awayScore += 1;

                AddEvent(MatchEventKind.DropGoal,
                    $"{(homeWins ? homeKicker.Player.Name : awayKicker.Player.Name)} lands the winning drop goal in extra time.",
                    homeWins ? TeamId(true) : TeamId(false));
            }

            FinishMatch();
            UpdateCamera(dt);
            return;
        }

        if (_flight)
        {
            UpdateFlight(dt);
            UpdateCamera(dt);
            return;
        }

        HandleInput(dt);
        MoveActors(dt);
        CheckTackles(dt);
        CheckTry();
        UpdateCamera(dt);
    }

    private void HandleInput(float dt)
    {
        var pass = Input.AnyPointerPressedIn(_passButton) || Input.IsNewKeyPress(Microsoft.Xna.Framework.Input.Keys.Space);
        var kick = Input.AnyPointerPressedIn(_kickButton) || Input.IsNewKeyPress(Microsoft.Xna.Framework.Input.Keys.K);
        var tacklePressed = Input.AnyPointerPressedIn(_tackleButton) ||
                            Input.IsNewKeyPress(Microsoft.Xna.Framework.Input.Keys.J);
        _sprintHeld = Input.AnyPointerIn(_sprintButton) ||
                      Input.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) ? 1f : 0f;

        var axis = Input.MoveAxis + _stickValue;
        if (axis.LengthSquared() > 1) axis.Normalize();

        if (_controlled is not null && axis.LengthSquared() > 0.001f)
        {
            var direction = AttackDirection(_homePossession);
            var move = new Vector3(-axis.Y * direction, 0, axis.X * direction);
            var speed = ActorSpeed(_controlled) * (1f + 0.22f * _sprintHeld);
            _controlled.Position += move * speed * dt;
            ClampToField(_controlled);

            if (_controlled == _carrier && _carrier is not null)
            {
                var gained = MathF.Abs(_carrier.Position.X - _carryStartX);
                if (gained > 0.01f)
                {
                    var stat = _carrier.IsHome ? _homeStats : _awayStats;
                    stat.Metres += (int)MathF.Round(gained);
                    PlayerStat(_carrier).Metres += (int)MathF.Round(gained);
                    _carryStartX = _carrier.Position.X;
                }
            }
        }

        _tacklePressed = _controlled is not null && _controlled.IsHome != _homePossession && tacklePressed;

        if (_carrier is null || _carrier.IsHome != _homePossession) return;

        if (pass) Pass(_carrier);
        else if (kick) Kick(_carrier, _goldenPoint && NearPosts(_carrier));
    }

    private bool _tacklePressed;

    private static float ActorSpeed(Actor actor) => 6.1f + actor.Player.Attributes.Pace / 99f * 2.7f;

    private void MoveActors(float dt)
    {
        var ball = _carrier?.Position ?? _ball;

        foreach (var actor in _actors)
        {
            if (actor == _controlled) continue;

            if (actor == _carrier)
            {
                if (actor.IsHome != _homePossession)
                {
                    RunAiCarrier(actor, dt);
                }
                else if (actor.IsHome == _homePossession && _controlled != _carrier)
                {
                    RunAiCarrier(actor, dt);
                }

                continue;
            }

            var attacking = actor.IsHome == _homePossession;
            if (attacking)
            {
                var target = AttackTarget(actor, ball);
                MoveTowards(actor, target, ActorSpeed(actor) * 0.92f, dt);
            }
            else
            {
                var chase = DefendersChasing(actor);
                var target = chase ? ball : DefenceTarget(actor, ball);
                MoveTowards(actor, target, ActorSpeed(actor) * (chase ? 1f : 0.8f), dt);
            }
        }

        if (_carrier is not null)
        {
            if (_carrier.IsHome == _homePossession && _controlled == _carrier)
            {
                // Controlled runner already moved by the stick.
            }
        }
    }

    private bool DefendersChasing(Actor actor)
    {
        var carrierPosition = _carrier?.Position ?? _ball;
        var nearest = _actors
            .Where(a => a.IsHome != actor.IsHome)
            .OrderBy(a => Vector3.DistanceSquared(a.Position, carrierPosition))
            .Take(3)
            .ToHashSet();

        return nearest.Contains(actor);
    }

    private Vector3 AttackTarget(Actor actor, Vector3 ball)
    {
        var slot = Sheet(actor.IsHome).Formation.SlotForShirt(actor.Shirt);
        var ox = slot?.AttackX ?? 0;
        var oy = slot?.AttackY ?? 0;
        var direction = AttackDirection(actor.IsHome);
        var target = ball + new Vector3(direction * ox * 0.85f, 0, oy * 0.85f);
        return new Vector3(Math.Clamp(target.X, 2, 98), 0, Math.Clamp(target.Z, 3, 65));
    }

    private Vector3 DefenceTarget(Actor actor, Vector3 ball)
    {
        var slot = Sheet(actor.IsHome).Formation.SlotForShirt(actor.Shirt);
        var ox = slot?.DefenceX ?? 0;
        var oy = slot?.DefenceY ?? 0;
        var direction = AttackDirection(actor.IsHome);
        var target = ball + new Vector3(direction * ox, 0, oy);
        return new Vector3(Math.Clamp(target.X, 2, 98), 0, Math.Clamp(target.Z, 3, 65));
    }

    private void RunAiCarrier(Actor actor, float dt)
    {
        var tryLine = TryLine(actor.IsHome);
        var direction = AttackDirection(actor.IsHome);
        var lane = 34f + MathF.Sin(_clock * 0.4f + actor.Shirt) * 9f;
        var target = new Vector3(tryLine - direction * 1.5f, 0, lane);

        var nearest = _actors
            .Where(a => a.IsHome != actor.IsHome)
            .OrderBy(a => Vector3.DistanceSquared(a.Position, actor.Position))
            .FirstOrDefault();

        var pressure = nearest is null ? 99f : Vector3.Distance(nearest.Position, actor.Position);

        if (pressure < 3.4f && _rng.NextDouble() < dt * 2.2) Pass(actor);
        else if (_tackle >= 5 && _rng.NextDouble() < dt * 1.4) Kick(actor, false);

        MoveTowards(actor, target, ActorSpeed(actor) * 0.96f, dt);
    }

    private void MoveTowards(Actor actor, Vector3 target, float speed, float dt)
    {
        var delta = target - actor.Position;
        delta.Y = 0;
        if (delta.LengthSquared() < 0.04f) return;

        delta.Normalize();
        actor.Position += delta * speed * dt;
        ClampToField(actor);
    }

    private static void ClampToField(Actor actor)
    {
        actor.Position.X = Math.Clamp(actor.Position.X, 1f, 99f);
        actor.Position.Z = Math.Clamp(actor.Position.Z, 2f, 66f);
        actor.Position.Y = 0;
    }

    private void CheckTackles(float dt)
    {
        if (_carrier is null) return;

        foreach (var defender in _actors.Where(a => a.IsHome != _carrier.IsHome).ToList())
        {
            defender.TackleCooldown = Math.Max(0, defender.TackleCooldown - dt);
            if (defender.TackleCooldown > 0) continue;

            var distance = Vector3.Distance(defender.Position, _carrier.Position);
            if (distance > 2.3f) continue;

            defender.TackleCooldown = 0.9f;
            AttemptTackle(defender);
            return;
        }
    }

    private void AttemptTackle(Actor defender)
    {
        var carrier = _carrier!;
        var tackling = defender.Player.Attributes.Tackling;
        var strength = carrier.Player.Attributes.Strength;
        var pace = carrier.Player.Attributes.Pace;

        var chance = 0.5 + (tackling - strength) / 240.0 + (_tacklePressed && defender == _controlled ? 0.22 : 0)
                     - pace / 700.0;
        chance = Math.Clamp(chance, 0.18, 0.93);

        var stat = defender.IsHome ? _homeStats : _awayStats;
        stat.Tackles++;
        PlayerStat(defender).Tackles++;

        if (_rng.NextDouble() > chance)
        {
            stat.MissedTackles++;
            PlayerStat(defender).MissedTackles++;
            var direction = AttackDirection(carrier.IsHome);
            carrier.Position.X += direction * 4f;
            ClampToField(carrier);
            ShowMessage($"{defender.Player.ShortName} slips off the tackle", 1.2f);
            return;
        }

        _tackle++;
        var carrierStats = carrier.IsHome ? _homeStats : _awayStats;
        carrierStats.Metres += 2;

        var penalty = _rng.NextDouble() < 0.045;
        if (penalty)
        {
            var offending = defender.IsHome ? _homeStats : _awayStats;
            offending.Penalties++;
            _tackle = 0;
            ShowMessage("Penalty — set restart", 1.6f);
            AddEvent(MatchEventKind.PenalityConceded, $"Penalty to {TeamName(carrier.IsHome)}. Set restart.", TeamId(carrier.IsHome));
        }

        if (_rng.NextDouble() < 0.012 && _injuries.Count < 3)
        {
            var (type, severity, weeks) = InjuryFactory.Create(_rng);
            _injuries.Add(new MatchInjury
            {
                PlayerId = carrier.Player.Id,
                PlayerName = carrier.Player.Name,
                TeamId = carrier.IsHome ? TeamId(true) : TeamId(false),
                Type = type,
                Severity = severity,
                WeeksOut = weeks
            });
        }

        if (_tackle >= 6)
        {
            ShowMessage("Six again — handover", 1.8f);
            Turnover(_carrier.Position);
        }
        else
        {
            _ball = new Vector3(_carrier.Position.X, 0.4f, _carrier.Position.Z);
            _pause = 0.45f;
            SelectNextCarrier();
        }
    }

    private void SelectNextCarrier()
    {
        _carrier = _actors
            .Where(a => a.IsHome == _homePossession)
            .OrderBy(a => Vector3.DistanceSquared(a.Position, _ball))
            .FirstOrDefault();

        SelectControlled();

        if (_carrier is not null) _carryStartX = _carrier.Position.X;
    }

    /// <summary>The human controls the ball carrier in attack, otherwise the nearest defender.</summary>
    private void SelectControlled()
    {
        _controlled = _homePossession == _playerIsHome
            ? _carrier
            : _actors
                .Where(a => a.IsHome == _playerIsHome)
                .OrderBy(a => Vector3.DistanceSquared(a.Position, _ball))
                .FirstOrDefault();
    }

    private void Turnover(Vector3 position)
    {
        var wasHome = _homePossession;
        _homePossession = !_homePossession;
        _tackle = 0;
        _ball = new Vector3(Math.Clamp(position.X, 4, 96), 0.4f, Math.Clamp(position.Z, 3, 65));

        var stats = wasHome ? _homeStats : _awayStats;
        stats.Sets++;

        ResetPositions();
        _carrier = Nearest(_homePossession, _ball);
        SelectControlled();
        if (_carrier is not null) _carryStartX = _carrier.Position.X;
    }

    private Actor BestKicker(bool home) => _actors
        .Where(a => a.IsHome == home)
        .OrderByDescending(a => a.Player.Attributes.GoalKicking + a.Player.Attributes.Kicking)
        .First();

    private bool NearPosts(Actor actor)
    {
        var line = TryLine(actor.IsHome);
        return MathF.Abs(actor.Position.X - line) < 32 && MathF.Abs(actor.Position.Z - 34) < 16;
    }

    private void Pass(Actor from)
    {
        var direction = AttackDirection(from.IsHome);
        var target = _actors
            .Where(a => a.IsHome == from.IsHome && a != from)
            .Where(a => (a.Position.X - from.Position.X) * direction > -4)
            .OrderBy(a => Vector3.Distance(a.Position, from.Position))
            .FirstOrDefault();

        if (target is null) return;

        if (_rng.NextDouble() < 0.07)
        {
            var stat = from.IsHome ? _homeStats : _awayStats;
            stat.Errors++;
            ShowMessage("Knock-on — handover", 1.8f);
            AddEvent(MatchEventKind.Error, $"{from.Player.ShortName} knocks on.", TeamId(from.IsHome));
            Turnover(from.Position);
            return;
        }

        _flight = true;
        _flightIsKick = false;
        _flightFrom = new Vector3(from.Position.X, 0.8f, from.Position.Z);
        _flightTo = new Vector3(target.Position.X, 0.8f, target.Position.Z);
        _flightElapsed = 0;
        _flightDuration = Math.Clamp(Vector3.Distance(_flightFrom, _flightTo) / 22f, 0.18f, 0.6f);
        _flightTarget = target;
        _carrier = null;
        _ball = _flightFrom;
    }

    private void Kick(Actor from, bool dropGoal)
    {
        if (dropGoal)
        {
            var chance = 0.28 + from.Player.Attributes.Kicking / 260.0;
            if (_rng.NextDouble() < chance)
            {
                if (from.IsHome) _homeScore += 1;
                else _awayScore += 1;

                AddEvent(MatchEventKind.DropGoal, $"{from.Player.Name} nails the drop goal!", TeamId(from.IsHome));
                ShowMessage("Drop goal!", 2f);
                FinishMatch();
            }
            else
            {
                ShowMessage("Drop goal missed", 1.4f);
                AddEvent(MatchEventKind.Commentary, $"{from.Player.Name} misses the drop goal.", null);
                Turnover(from.Position);
            }

            return;
        }

        var direction = AttackDirection(from.IsHome);
        var distance = 32f + (float)_rng.NextDouble() * 22f;
        var landingX = Math.Clamp(from.Position.X + direction * distance, 6, 94);
        var landingZ = Math.Clamp(from.Position.Z + (float)(_rng.NextDouble() * 18 - 9), 4, 64);

        _flight = true;
        _flightIsKick = true;
        _flightFrom = new Vector3(from.Position.X, 0.8f, from.Position.Z);
        _flightTo = new Vector3(landingX, 0.4f, landingZ);
        _flightElapsed = 0;
        _flightDuration = 1.15f;
        _flightTarget = null;
        _carrier = null;
        _ball = _flightFrom;

        var stat = from.IsHome ? _homeStats : _awayStats;
        stat.Kicks++;
        stat.Sets++;
        stat.Completions++;
        AddEvent(MatchEventKind.Commentary, $"{from.Player.ShortName} kicks downfield.", TeamId(from.IsHome));
    }

    private void UpdateFlight(float dt)
    {
        _flightElapsed += dt;
        var t = Math.Clamp(_flightElapsed / _flightDuration, 0f, 1f);
        _ball = Vector3.Lerp(_flightFrom, _flightTo, t) + new Vector3(0, MathF.Sin(t * MathF.PI) * 3.4f, 0);

        if (_flightDuration > 0.6f) _ball.Y = MathF.Max(0.4f, _ball.Y);

        if (t < 1) return;

        _flight = false;
        if (_flightIsKick)
        {
            _homePossession = !_homePossession;
            _tackle = 0;
            _ball = new Vector3(_flightTo.X, 0.4f, _flightTo.Z);
            ResetPositions();
            _carrier = Nearest(_homePossession, _ball);
            SelectControlled();
            if (_carrier is not null) _carryStartX = _carrier.Position.X;
        }
        else
        {
            _carrier = _flightTarget;
            _ball = new Vector3(_flightTo.X, 0.4f, _flightTo.Z);
            SelectControlled();
            if (_carrier is not null) _carryStartX = _carrier.Position.X;
        }
    }

    private void CheckTry()
    {
        if (_carrier is null) return;

        var line = TryLine(_carrier.IsHome);
        var scored = _carrier.IsHome ? _carrier.Position.X >= line : _carrier.Position.X <= line;
        if (!scored) return;

        ScoreTry(_carrier);
    }

    private void ScoreTry(Actor scorer)
    {
        var home = scorer.IsHome;
        if (home) _homeScore += 4;
        else _awayScore += 4;

        if (home) _homeTries++;
        else _awayTries++;

        PlayerStat(scorer).Tries++;
        AddEvent(MatchEventKind.Try, $"TRY! {scorer.Player.Name} scores for {TeamName(home)}.", TeamId(home));

        // Conversion.
        var kicker = BestKicker(home);
        var chance = Math.Clamp(0.48 + kicker.Player.Attributes.GoalKicking / 250.0, 0.35, 0.95);
        if (MathF.Abs(scorer.Position.Z - 34) > 16) chance -= 0.12;

        if (_rng.NextDouble() < chance)
        {
            if (home) _homeScore += 2;
            else _awayScore += 2;

            PlayerStat(kicker).Goals++;
            AddEvent(MatchEventKind.Conversion, $"{kicker.Player.ShortName} converts.", TeamId(home));
            ShowMessage($"TRY! {scorer.Player.ShortName} scores and converts", 2.4f);
        }
        else
        {
            ShowMessage($"TRY! {scorer.Player.ShortName} scores — conversion missed", 2.4f);
        }

        _pause = 2.2f;

        if (_goldenPoint)
        {
            FinishMatch();
            return;
        }

        Kickoff(!home);
    }

    private void Kickoff(bool homeReceives)
    {
        _homePossession = homeReceives;
        _tackle = 0;
        _ball = new Vector3(50, 0.4f, 34);
        ResetPositions();
        _carrier = Nearest(homeReceives, _ball);
        SelectControlled();
        if (_carrier is not null) _carryStartX = _carrier.Position.X;
    }

    private void ResetPositions()
    {
        foreach (var actor in _actors)
        {
            var attacking = actor.IsHome == _homePossession;
            var slot = Sheet(actor.IsHome).Formation.SlotForShirt(actor.Shirt);
            var direction = AttackDirection(actor.IsHome);

            var ox = slot is null ? 0 : attacking ? slot.AttackX : slot.DefenceX;
            var oz = slot is null ? 0 : attacking ? slot.AttackY : slot.DefenceY;

            actor.Position = new Vector3(
                Math.Clamp(_ball.X + direction * ox * 0.8f, 2, 98),
                0,
                Math.Clamp(_ball.Z + oz * 0.9f, 3, 65));
        }
    }

    private Actor? Nearest(bool home, Vector3 position) => _actors
        .Where(a => a.IsHome == home)
        .OrderBy(a => Vector3.DistanceSquared(a.Position, position))
        .FirstOrDefault();

    private PlayerMatchStats PlayerStat(Actor actor) => _playerStats[actor.Player.Id];

    private void AddEvent(MatchEventKind kind, string text, Guid? teamId)
    {
        _events.Add(new MatchEvent
        {
            Minute = (int)Math.Clamp(_clock, 0, 90),
            Kind = kind,
            Text = text,
            TeamId = teamId,
            HomeScore = _homeScore,
            AwayScore = _awayScore
        });
    }

    private void FinishMatch()
    {
        if (_finished) return;
        _finished = true;

        AddEvent(MatchEventKind.FullTime, $"Full time: {TeamName(true)} {_homeScore}-{_awayScore} {TeamName(false)}.",
            null);

        var total = Math.Max(1f, _homePossessionTime + _awayPossessionTime);
        _homeStats.Possession = (int)Math.Round(_homePossessionTime / total * 100);
        _awayStats.Possession = 100 - _homeStats.Possession;
        _homeStats.Tries = _homeTries;
        _awayStats.Tries = _awayTries;
        _homeStats.Sets = Math.Max(1, _homeStats.Sets);
        _awayStats.Sets = Math.Max(1, _awayStats.Sets);

        var man = _playerStats.Values
            .OrderByDescending(s => s.Tries * 8 + s.Goals * 3 + s.Tackles * 0.4)
            .FirstOrDefault();

        if (man is not null) man.ManOfTheMatch = true;

        _result = new MatchResult
        {
            Competition = _setup.CompetitionName,
            RoundName = $"Round {_career.Round}",
            Venue = _career.TryTeam(_setup.Home.TeamId)?.StadiumName ?? "",
            Weather = _setup.Weather.Kind,
            HomeTeamId = _setup.Home.TeamId,
            AwayTeamId = _setup.Away.TeamId,
            HomeTeamName = _setup.Home.TeamName,
            AwayTeamName = _setup.Away.TeamName,
            HomeScore = _homeScore,
            AwayScore = _awayScore,
            HomeTries = _homeTries,
            AwayTries = _awayTries,
            GoldenPoint = _goldenPoint,
            HomeStats = _homeStats,
            AwayStats = _awayStats,
            Events = _events,
            PlayerStats = _playerStats.Values.ToList(),
            Injuries = _injuries
        };

        ShowMessage($"Full time: {_homeScore}-{_awayScore}", 60f);
    }

    private void UpdateCamera(float dt)
    {
        var focus = _carrier?.Position ?? _ball;
        var direction = AttackDirection(_homePossession);

        var desired = new Vector3(focus.X - direction * 16f, 12.5f, focus.Z + 0.01f);
        var blend = 1f - MathF.Exp(-4.5f * dt);
        _cameraPosition = Vector3.Lerp(_cameraPosition, desired, blend);
        _cameraLook = Vector3.Lerp(_cameraLook, focus + new Vector3(direction * 9f, 0, 0), blend);
    }
}
