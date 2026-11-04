using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Model;

namespace RugbyLeague.Core.Sim;

/// <summary>
/// Simulates a full eighty-minute match from player skills, tactics, formation shape, weather and
/// home advantage. The engine is deterministic for a given <see cref="MatchSetup.Seed"/> and records
/// a frame timeline that the top-down viewer replays.
/// </summary>
public static class MatchEngine
{
    public static MatchResult Simulate(MatchSetup setup) => new Simulation(setup).Run();
}

internal sealed class Simulation
{
    private const double HalfLength = 40;
    private const double FullLength = 80;
    private const double GoldenPointLength = 10;
    private const int BenchSize = 4;

    private readonly MatchSetup _setup;
    private readonly Random _rng;
    private readonly Random _cosmeticRng;
    private readonly Side _home;
    private readonly Side _away;
    private readonly MatchResult _result = new();
    private readonly bool _captureFrames;

    private double _clock;
    private Side _possession;
    private double _fieldPos = 40;
    private float _ballY = 34f;
    private bool _goldenPoint;
    private bool _secondHalf;
    private int _homeSinBinUntil = -1;
    private int _awaySinBinUntil = -1;
    private readonly List<string> _pendingComments = new();

    public Simulation(MatchSetup setup)
    {
        _setup = setup;
        _rng = new Random(setup.Seed);
        _cosmeticRng = new Random(setup.Seed * 31 + 7);
        _captureFrames = setup.CaptureFrames;

        _home = new Side(setup.Home, true, setup.HomeTactics);
        _away = new Side(setup.Away, false, setup.AwayTactics);
        _possession = _rng.NextDouble() < 0.5 ? _home : _away;

        _result.Competition = setup.CompetitionName;
        _result.Weather = setup.Weather.Kind;
        _result.HomeTeamId = setup.Home.TeamId;
        _result.AwayTeamId = setup.Away.TeamId;
        _result.HomeTeamName = setup.Home.TeamName;
        _result.AwayTeamName = setup.Away.TeamName;
        _result.Venue = setup.Home.TeamName + " Stadium";
    }

    public MatchResult Run()
    {
        _result.Events.Add(Event(MatchEventKind.KickOff, 0,
            $"{_possession.Sheet.TeamName} will receive the kick-off in {_setup.Weather.Label.ToLowerInvariant()} conditions."));

        KickOff(_secondHalf ? 40 : 0);

        var safety = 0;
        while (safety++ < 4000)
        {
            if (!_secondHalf && _clock >= HalfLength)
            {
                _secondHalf = true;
                _clock = HalfLength;
                AddFrame("Half time", 1.6f, 0);
                _result.Events.Add(Event(MatchEventKind.HalfTime, 40,
                    $"Half time: {_home.Sheet.TeamName} {_result.HomeScore}-{_result.AwayScore} {_away.Sheet.TeamName}."));
                ResetConditionAtHalfTime();
                _possession = _possession == _home ? _away : _home;
                KickOff(40);
                continue;
            }

            var limit = _goldenPoint ? FullLength + GoldenPointLength : FullLength;
            if (_clock >= limit)
            {
                if (!_goldenPoint && _setup.MustHaveWinner && _result.HomeScore == _result.AwayScore)
                {
                    _goldenPoint = true;
                    _result.GoldenPoint = true;
                    _result.Events.Add(Event(MatchEventKind.GoldenPoint, 80,
                        "Scores are level. Golden point: the first score wins it."));
                    _possession = _rng.NextDouble() < 0.5 ? _home : _away;
                    KickOff(80);
                    continue;
                }

                if (_goldenPoint && _result.HomeScore == _result.AwayScore)
                {
                    ResolveShootout();
                }

                break;
            }

            PlaySet();
        }

        Finalise();
        return _result;
    }

    // ---------------------------------------------------------------- core possession loop

    private void PlaySet()
    {
        var attack = _possession;
        var defence = attack == _home ? _away : _home;
        attack.Stats.Sets++;

        var tackle = 0;
        var setContinues = true;

        while (setContinues && tackle < 6)
        {
            tackle++;
            _clock += 0.42 + _rng.NextDouble() * 0.12;

            if (TryInterchange(attack)) { /* continues */ }
            if (TryInterchange(defence)) { /* continues */ }

            var condition = attack.ConditionOnField();
            var edge = Math.Clamp((attack.AttackRating(defence, IsSinBinned(defence)) + HomeAdvantage(attack)
                                   - defence.DefenceRating()) / 100.0, -0.35, 0.35);

            var carry = SimulateCarry(attack, defence, edge, tackle, condition);

            attack.Stats.Metres += (int)Math.Max(0, carry.Metres);
            attack.Stats.PossessionTicks++;

            TryMatchInjuries(attack, defence);

            if (carry.Try)
            {
                attack.Stats.Completions++;
                ConvertTry(attack, edge, carry.Player);
                RestartAfterScore(attack, defence);
                return;
            }

            if (carry.Error)
            {
                attack.Stats.Errors++;
                _pendingComments.Add($"{carry.Player?.ShortName ?? "The ball carrier"} spills it. Turnover.");
                _result.Events.Add(Event(MatchEventKind.Error, Minute(), $"{attack.Sheet.TeamName} turn the ball over: handling error by {carry.Player?.ShortName}."));
                AddFrame("Handling error", 1.8f, tackle);
                Handover(attack, defence, 1.0);
                return;
            }

            if (carry.Penalty)
            {
                defence.Stats.Penalties++;
                _result.Events.Add(Event(MatchEventKind.PenalityConceded, Minute(),
                    $"Penalty to {attack.Sheet.TeamName}: {defence.Sheet.TeamName} caught offside."));
                if (TrySinBin(defence))
                {
                    AddFrame("Sin bin", 2.0f, tackle);
                }

                // Twelve-a-side against a tiring defence invites a snap drop goal.
                if (TryDropGoal(attack))
                {
                    Handover(attack, defence, 0.6);
                    return;
                }

                _fieldPos = Math.Clamp(_fieldPos + 10, 5, 95);
                AddFrame("Penalty", 1.8f, tackle);

                if (ShouldTakeShotAtGoal(attack))
                {
                    AttemptPenaltyGoal(attack);
                    Handover(attack, defence, 0.5);
                    return;
                }

                tackle = 0; // fresh set of six
                continue;
            }

            _fieldPos = Math.Clamp(_fieldPos + carry.Metres, 0, 99.5);

            if (carry.LineBreak)
            {
                attack.Stats.LineBreaks++;
                _result.Events.Add(Event(MatchEventKind.LineBreak, Minute(),
                    $"Line break! {carry.Player?.ShortName} slices through for {attack.Sheet.TeamName}."));
            }

            _ballY = Math.Clamp(_ballY + (float)(_rng.NextDouble() * 9 - 4.5) * (float)(carry.Metres > 12 ? 1.6 : 1.0), 4, 64);
            AddFrame(carry.Metres > 14 ? "Strong carry" : "Carry", 1.5f, tackle);

            if (tackle == 6) setContinues = false;
        }

        if (setContinues) return;

        attack.Stats.Completions++;

        if (TryDropGoal(attack))
        {
            Handover(attack, defence, 0.6);
            return;
        }

        // Last tackle: kick.
        Kick(attack, defence);
    }

    private readonly record struct CarryOutcome(double Metres, bool Try, bool Error, bool Penalty, bool LineBreak, Player? Player);

    private CarryOutcome SimulateCarry(Side attack, Side defence, double edge, int tackle, double condition)
    {
        var carrier = attack.PickCarrier(_rng);
        var formation = attack.Sheet.Formation;
        var middleCarry = _rng.NextDouble() < (0.45 + (attack.Tactics.Style == PlayingStyle.ForwardPower ? 0.2 : 0));
        var shapeBonus = middleCarry ? (formation.MiddleThreat - 1f) * 26 : (formation.EdgeThreat - 1f) * 26;

        var styleBonus = attack.Tactics.Style switch
        {
            PlayingStyle.ForwardPower => middleCarry ? 4.0 : -2.0,
            PlayingStyle.Expansive => middleCarry ? -1.0 : 2.5,
            PlayingStyle.KickingGame => -0.5,
            PlayingStyle.Attritional => 0.5,
            _ => 0
        };

        var fatigue = attack.FatigueOnField();
        var metres = 5.6
                     + edge * 14
                     + shapeBonus
                     + styleBonus
                     + (middleCarry ? attack.Tactics.Offloads / 100.0 * 0.8 : attack.Tactics.Width / 100.0 * 0.8)
                     + (_rng.NextDouble() * 9 - 3.5)
                     - fatigue * 2.6
                     - (condition < 0.75 ? 1.5 : 0);

        var breakChance = 0.034 * (1 + edge * 1.1)
                          * (attack.Tactics.Style == PlayingStyle.Expansive ? 1.18 : 1.0)
                          * (middleCarry ? 1.0 : formation.EdgeThreat)
                          * (1 - fatigue * 0.25);

        var lineBreak = _rng.NextDouble() < Math.Clamp(breakChance, 0.005, 0.2);
        if (lineBreak) metres += 9 + _rng.NextDouble() * 20;

        metres = Math.Clamp(metres, -3, 55);

        var newPos = _fieldPos + metres;
        var tryScored = false;
        if (newPos >= 99)
        {
            var tryChance = 0.34 + edge * 0.6 + (lineBreak ? 0.12 : 0);
            tryScored = _rng.NextDouble() < Math.Clamp(tryChance, 0.15, 0.94);
            if (tryScored) metres = 99 - _fieldPos;
        }
        else if (_fieldPos > 90 && _rng.NextDouble() < 0.07 + Math.Max(0, edge) * 0.09)
        {
            tryScored = true;
            metres = 99 - _fieldPos;
        }

        if (tryScored)
            return new CarryOutcome(metres, true, false, false, lineBreak, carrier);

        var handling = attack.HandlingRating();
        var errorChance = 0.023
                          * (1 + _setup.Weather.ErrorModifier)
                          * (1 + (65 - handling) / 120.0)
                          * (1 + fatigue * 1.1)
                          * (1 + attack.Tactics.Offloads / 400.0);

        if (_rng.NextDouble() < Math.Clamp(errorChance, 0.002, 0.14))
        {
            attack.RecordError(carrier);
            return new CarryOutcome(0, false, true, false, false, carrier);
        }

        var penaltyChance = 0.017
                            * (1 + (60 - defence.DisciplineRating()) / 130.0)
                            * (1 - defence.Tactics.LineSpeed / 500.0);
        if (_rng.NextDouble() < Math.Clamp(penaltyChance, 0.002, 0.06))
        {
            return new CarryOutcome(0, false, false, true, lineBreak, carrier);
        }

        attack.RecordCarry(carrier, (int)Math.Max(0, metres), lineBreak);
        defence.RecordTackle(_rng);
        return new CarryOutcome(metres, false, false, false, lineBreak, carrier);
    }

    private void Kick(Side attack, Side defence)
    {
        attack.Stats.Kicks++;
        var kicker = attack.BestKicker(_rng);
        var kickSkill = kicker?.Attributes.Kicking ?? 55;

        var fortyTwentyChance = _fieldPos is > 25 and < 60 ? 0.03 + Math.Max(0, kickSkill - 65) / 420.0 : 0;
        if (_rng.NextDouble() < fortyTwentyChance)
        {
            attack.Stats.FortyTwenties++;
            _result.Events.Add(Event(MatchEventKind.FortyTwenty, Minute(),
                $"Forty-twenty from {kicker?.ShortName}! {attack.Sheet.TeamName} keep the ball in great field position."));
            _fieldPos = 88;
            _ballY = 34;
            AddFrame("Forty-twenty", 2.4f, 0);
            return;
        }

        if (_fieldPos > 55)
        {
            // Attacking kick: bomb or grubber.
            var contest = 0.24 + (kickSkill - 60) / 350.0 + attack.Tactics.KickEarly / 900.0;
            if (_rng.NextDouble() < contest)
            {
                _result.Events.Add(Event(MatchEventKind.Commentary, Minute(),
                    $"{kicker?.ShortName} hoists a bomb and {attack.Sheet.TeamName} regather."));
                _fieldPos = Math.Clamp(_fieldPos + 6, 5, 95);
                AddFrame("Bomb regathered", 2.2f, 0);
                return;
            }

            if (_rng.NextDouble() < 0.35)
            {
                _result.Events.Add(Event(MatchEventKind.Commentary, Minute(),
                    $"Grubber kick forces a goal-line drop-out. {attack.Sheet.TeamName} will attack again."));
                _fieldPos = Math.Clamp(_fieldPos + 12, 10, 96);
                AddFrame("Goal-line drop-out", 2.2f, 0);
                return;
            }

            Handover(attack, defence, 0.85);
            AddFrame("Kick caught", 2.0f, 0);
            return;
        }

        var distance = 22 + _rng.NextDouble() * 26 + (kickSkill - 60) / 4.0;
        var landing = Math.Clamp(_fieldPos + distance, 10, 92);
        _fieldPos = 100 - landing;
        _ballY = 34;
        AddFrame("Long kick", 2.2f, 0);
        Handover(attack, defence, 0.0, keepFieldPos: true);
    }

    private void Handover(Side attack, Side defence, double deepFactor, bool keepFieldPos = false)
    {
        if (!keepFieldPos)
            _fieldPos = Math.Clamp(100 - _fieldPos + (deepFactor > 0.9 ? 8 : 0), 5, 92);

        _possession = defence;
        _ballY = 34;
        _result.Events.Add(Event(MatchEventKind.Handover, Minute(),
            $"{defence.Sheet.TeamName} take possession {Math.Round(100 - _fieldPos)}m out from their own line."));
        AddFrame("Handover", 1.4f, 0);
    }

    private void RestartAfterScore(Side scorers, Side conceders)
    {
        _possession = conceders;
        _fieldPos = 38 + _rng.NextDouble() * 10;
        _ballY = 34;
        AddFrame("Restart", 2.4f, 0);
    }

    private void KickOff(double atMinute)
    {
        _fieldPos = 38 + _rng.NextDouble() * 12;
        _ballY = 20 + (float)_rng.NextDouble() * 28;
        AddFrame("Kick-off", 2.2f, 0);
    }

    // ---------------------------------------------------------------- scoring

    private void ConvertTry(Side attack, double edge, Player? scorer)
    {
        attack.Stats.Tries++;
        attack.RecordTry(scorer);
        _ballY = Math.Clamp(_ballY, 10, 58);

        string sideName = attack.Sheet.TeamName;
        AddScore(attack, 4);
        _result.Events.Add(Event(MatchEventKind.Try, Minute(),
            $"TRY! {scorer?.ShortName ?? "The winger"} goes over for {sideName}. {_result.HomeScore}-{_result.AwayScore}.",
            attack.Sheet.TeamId));
        AddFrame($"Try {scorer?.LastName}", 3.0f, 0);

        _clock += 0.8;
        var kicker = attack.BestGoalKicker();
        var goalKick = kicker?.Attributes.GoalKicking ?? 50;
        var wide = Math.Abs(_ballY - 34) / 34f;
        var goalChance = 0.93 - wide * 0.24 + (goalKick - 70) / 260.0 - _setup.Weather.KickingModifier;

        if (_rng.NextDouble() < Math.Clamp(goalChance, 0.35, 0.99))
        {
            AddScore(attack, 2);
            attack.RecordGoal(kicker);
            _result.Events.Add(Event(MatchEventKind.Conversion, Minute(),
                $"{kicker?.ShortName} converts. {_result.HomeScore}-{_result.AwayScore}.", attack.Sheet.TeamId));
        }
        else
        {
            _result.Events.Add(Event(MatchEventKind.ConversionMissed, Minute(),
                $"{kicker?.ShortName} pushes the conversion wide. {_result.HomeScore}-{_result.AwayScore}.", attack.Sheet.TeamId));
        }

        AddFrame("Conversion", 2.2f, 0);
        _result.Events.Add(Event(MatchEventKind.Commentary, Minute(), $"{sideName} will restart."));
    }

    private bool ShouldTakeShotAtGoal(Side attack)
    {
        var distance = 100 - _fieldPos;
        if (distance > 42) return false;
        var margin = attack == _home ? _result.HomeScore - _result.AwayScore : _result.AwayScore - _result.HomeScore;
        var late = _clock > 65 || _goldenPoint;
        if (late && margin is >= 0 and <= 4) return true;
        if (distance < 24) return true;
        return _rng.NextDouble() < 0.35;
    }

    private void AttemptPenaltyGoal(Side attack)
    {
        var distance = 100 - _fieldPos;
        var kicker = attack.BestGoalKicker();
        var skill = kicker?.Attributes.GoalKicking ?? 55;
        var chance = 0.99 - distance * 0.0095 + (skill - 70) / 300.0 - _setup.Weather.KickingModifier;
        attack.Stats.Kicks++;

        if (_rng.NextDouble() < Math.Clamp(chance, 0.2, 0.98))
        {
            AddScore(attack, 2);
            attack.RecordGoal(kicker);
            _result.Events.Add(Event(MatchEventKind.PenaltyGoal, Minute(),
                $"{kicker?.ShortName} lands the penalty goal from {Math.Round(distance)}m. {_result.HomeScore}-{_result.AwayScore}.", attack.Sheet.TeamId));
        }
        else
        {
            _result.Events.Add(Event(MatchEventKind.PenaltyMissed, Minute(),
                $"{kicker?.ShortName} misses the penalty attempt from {Math.Round(distance)}m.", attack.Sheet.TeamId));
        }
        AddFrame("Penalty goal", 2.2f, 0);
    }

    private bool TryDropGoal(Side attack)
    {
        var distance = 100 - _fieldPos;
        if (distance > 38) return false;

        var margin = attack == _home ? _result.HomeScore - _result.AwayScore : _result.AwayScore - _result.HomeScore;
        var late = _goldenPoint || _clock > 76;
        if (!late || margin > 2 || margin < -3) return false;

        AttemptDropGoal(attack);
        return true;
    }

    private void AttemptDropGoal(Side attack)
    {
        var distance = 100 - _fieldPos;
        var kicker = attack.BestKicker(_rng);
        var skill = (kicker?.Attributes.Kicking ?? 55) + (kicker?.Attributes.GoalKicking ?? 40) * 0.4;
        var chance = 0.62 - distance * 0.008 + (skill - 75) / 300.0;

        if (_rng.NextDouble() < Math.Clamp(chance, 0.12, 0.85))
        {
            AddScore(attack, 1);
            attack.RecordDropGoal(kicker);
            _result.Events.Add(Event(MatchEventKind.DropGoal, Minute(),
                $"DROP GOAL! {kicker?.ShortName} nails it from {Math.Round(distance)}m. {_result.HomeScore}-{_result.AwayScore}.", attack.Sheet.TeamId));
            AddFrame("Drop goal", 2.6f, 0);
        }
        else
        {
            _result.Events.Add(Event(MatchEventKind.DropGoalMissed, Minute(),
                $"{kicker?.ShortName} misses a drop-goal attempt.", attack.Sheet.TeamId));
            Handover(attack, attack == _home ? _away : _home, 1.0);
        }
    }

    private void AddScore(Side side, int points)
    {
        if (side == _home) _result.HomeScore += points;
        else _result.AwayScore += points;
    }

    private void ResolveShootout()
    {
        _result.Events.Add(Event(MatchEventKind.Commentary, 90, "Still level after golden point. Drop-goal shootout decides it."));
        for (var i = 0; i < 10; i++)
        {
            var side = i % 2 == 0 ? _home : _away;
            var opponent = side == _home ? _away : _home;
            var skill = side.BestGoalKicker()?.Attributes.GoalKicking ?? 50;
            var scored = _rng.NextDouble() < 0.5 + (skill - 60) / 400.0;

            if (scored)
            {
                _result.Events.Add(Event(MatchEventKind.DropGoal, 90,
                    $"{side.BestGoalKicker()?.ShortName} lands the shootout kick for {side.Sheet.TeamName}."));
                AddScore(side, 1);
                return;
            }

            _result.Events.Add(Event(MatchEventKind.DropGoalMissed, 90,
                $"{opponent.Sheet.TeamName} survive as the kick drifts wide."));
        }

        // Last resort: a coin toss keeps the competition moving.
        AddScore(_home, 1);
    }

    // ---------------------------------------------------------------- discipline, injury, rotation

    private void TryMatchInjuries(Side attack, Side defence)
    {
        TryInjury(attack, 0.0060);
        TryInjury(defence, 0.0040);
    }

    private void TryInjury(Side side, double chance)
    {
        if (side.OnField.Count == 0 || _rng.NextDouble() > chance) return;

        var player = side.OnField[_rng.Next(side.OnField.Count)];
        var (type, severity, weeks) = InjuryFactory.Create(_rng);

        _result.Injuries.Add(new MatchInjury
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            TeamId = side.Sheet.TeamId,
            Type = type,
            Severity = severity,
            WeeksOut = weeks
        });

        _result.Events.Add(Event(MatchEventKind.Injury, Minute(),
            $"{player.ShortName} leaves the field with {type.ToLowerInvariant()} ({weeks} weeks).", side.Sheet.TeamId));

        side.OnField.Remove(player);
        var replacement = side.Bench.FirstOrDefault(p => p.IsAvailable);
        if (replacement is not null)
        {
            side.Bench.Remove(replacement);
            side.OnField.Add(replacement);
        }
    }

    private bool TrySinBin(Side defence)
    {
        if (_rng.NextDouble() > 0.055) return false;
        var until = (int)_clock + 10;
        if (defence == _home) _homeSinBinUntil = until;
        else _awaySinBinUntil = until;

        _result.Events.Add(Event(MatchEventKind.SinBin, Minute(),
            $"{defence.Sheet.TeamName} down to twelve men for ten minutes.", defence.Sheet.TeamId));
        return true;
    }

    private bool IsSinBinned(Side side) =>
        side == _home ? _homeSinBinUntil > _clock : _awaySinBinUntil > _clock;

    private bool TryInterchange(Side side)
    {
        if (side.Bench.Count == 0 || side.InterchangesUsed >= 8) return false;

        var window = (int)Math.Floor(_clock) is 20 or 40 or 55 or 65 or 70 or 75;
        if (!window || _rng.NextDouble() > 0.55) return false;

        var off = side.OnField
            .Where(p => p.Position is PlayerPosition.Prop or PlayerPosition.SecondRow or PlayerPosition.Lock or PlayerPosition.Hooker)
            .OrderByDescending(p => side.FatigueOf(p))
            .FirstOrDefault() ?? side.OnField.OrderByDescending(side.FatigueOf).FirstOrDefault();

        var on = side.Bench.FirstOrDefault(p => p.IsAvailable);
        if (off is null || on is null) return false;

        side.Bench.Remove(on);
        side.OnField.Remove(off);
        side.OnField.Add(on);
        side.Bench.Add(off);
        side.InterchangesUsed++;
        side.FatigueRelief(0.35);

        _result.Events.Add(Event(MatchEventKind.Interchange, Minute(),
            $"{side.Sheet.TeamName} interchange: {on.ShortName} on for {off.ShortName}."));
        return true;
    }

    private void ResetConditionAtHalfTime()
    {
        _home.FatigueRelief(0.35);
        _away.FatigueRelief(0.35);
    }

    // ---------------------------------------------------------------- frames

    private void AddFrame(string action, float replaySeconds, int tackle)
    {
        if (!_captureFrames) return;

        var frame = new MatchFrame
        {
            Minute = (float)Math.Round(_clock, 1),
            BallX = BallAbsoluteX(),
            BallY = _ballY,
            PossessionTeamId = _possession.Sheet.TeamId,
            Tackle = tackle,
            Action = action,
            ReplaySeconds = replaySeconds
        };

        var attackDir = _possession.IsHome ? 1f : -1f;

        foreach (var side in new[] { _home, _away })
        {
            var attacking = side == _possession;
            foreach (var player in side.OnField)
            {
                var slot = side.Sheet.Formation.SlotForShirt(player.ShirtNumber)
                           ?? side.Sheet.Formation.Slots.FirstOrDefault();

                var ox = slot is null ? 0 : attacking ? slot.AttackX : slot.DefenceX;
                var oy = slot is null ? 0 : attacking ? slot.AttackY : slot.DefenceY;

                var jitterX = (float)(_cosmeticRng.NextDouble() * 2.4 - 1.2);
                var jitterY = (float)(_cosmeticRng.NextDouble() * 2.4 - 1.2);

                frame.Players.Add(new FramePlayer
                {
                    TeamId = side.Sheet.TeamId,
                    Shirt = player.ShirtNumber,
                    X = Math.Clamp(frame.BallX + attackDir * ox + jitterX, 1, 99),
                    Y = Math.Clamp(frame.BallY + attackDir * oy + jitterY, 2, 66)
                });
            }
        }

        _result.Frames.Add(frame);
    }

    private float BallAbsoluteX() => (float)(_possession.IsHome ? _fieldPos : 100 - _fieldPos);

    // ---------------------------------------------------------------- finalisation

    private void Finalise()
    {
        _result.HomeStats = _home.Stats;
        _result.AwayStats = _away.Stats;

        _result.Events.Add(Event(MatchEventKind.FullTime, (int)Math.Clamp(_clock, 80, 90),
            $"Full time: {_home.Sheet.TeamName} {_result.HomeScore}-{_result.AwayScore} {_away.Sheet.TeamName}."));

        var totalTicks = Math.Max(1, _home.Stats.PossessionTicks + _away.Stats.PossessionTicks);
        _home.Stats.Possession = (int)Math.Round(_home.Stats.PossessionTicks * 100.0 / totalTicks);
        _away.Stats.Possession = 100 - _home.Stats.Possession;

        foreach (var side in new[] { _home, _away })
        {
            foreach (var runtime in side.Runtimes.Values)
            {
                _result.PlayerStats.Add(new PlayerMatchStats
                {
                    PlayerId = runtime.Player.Id,
                    TeamId = side.Sheet.TeamId,
                    Name = runtime.Player.Name,
                    Tries = runtime.Tries,
                    Goals = runtime.Goals,
                    DropGoals = runtime.DropGoals,
                    Metres = runtime.Metres,
                    Tackles = runtime.Tackles,
                    MissedTackles = runtime.MissedTackles
                });
            }
        }

        _result.HomeTries = _home.Stats.Tries;
        _result.AwayTries = _away.Stats.Tries;

        var winner = _result.WinnerId;
        var motm = _result.PlayerStats
            .OrderByDescending(p => p.Tries * 40 + p.Metres * 0.32 + p.Tackles * 0.55 +
                                    (winner != Guid.Empty && p.TeamId == winner ? 12 : 0))
            .FirstOrDefault();

        if (motm is not null) motm.ManOfTheMatch = true;

        AddFrame("Full time", 2.4f, 0);
    }

    private MatchEvent Event(MatchEventKind kind, int minute, string text, Guid? teamId = null) => new()
    {
        Kind = kind,
        Minute = minute,
        Text = text,
        TeamId = teamId,
        HomeScore = _result.HomeScore,
        AwayScore = _result.AwayScore
    };

    private int Minute() => (int)Math.Floor(Math.Min(_clock, 90));

    private double HomeAdvantage(Side attack) =>
        attack.IsHome ? 2.2 + _home.Sheet.Starters.Average(p => p.Form) / 100.0 : 0;

    // ---------------------------------------------------------------- side state

    private sealed class PlayerRuntime
    {
        public Player Player = null!;
        public double Fatigue;
        public int Tries;
        public int Goals;
        public int DropGoals;
        public int Metres;
        public int Tackles;
        public int MissedTackles;
        public int Errors;
        public int Carries;
    }

    private sealed class Side
    {
        public readonly TeamSheet Sheet;
        public readonly bool IsHome;
        public readonly TacticalSettings Tactics;
        public readonly MatchTeamStats Stats = new();
        public readonly Dictionary<Guid, PlayerRuntime> Runtimes = new();
        public readonly List<Player> OnField = new();
        public readonly List<Player> Bench = new();
        public int InterchangesUsed;

        public Side(TeamSheet sheet, bool isHome, TacticalSettings tactics)
        {
            Sheet = sheet;
            IsHome = isHome;
            Tactics = tactics;

            foreach (var player in sheet.Starters.Where(p => p.IsAvailable))
            {
                OnField.Add(player);
                Runtimes[player.Id] = new PlayerRuntime { Player = player };
            }

            foreach (var player in sheet.Bench.Where(p => p.IsAvailable).Take(BenchSize))
            {
                Bench.Add(player);
                Runtimes[player.Id] = new PlayerRuntime { Player = player };
            }
        }

        public double AttackRating(Side defence, bool defenceSinBinned)
        {
            if (OnField.Count == 0) return 40;

            var total = 0.0;
            foreach (var player in OnField)
            {
                var role = Sheet.Formation.SlotForShirt(player.ShirtNumber)?.Role ?? player.Position;
                total += PositionalAttack(player, role)
                         * (0.86 + player.Form / 100.0 * 0.14)
                         * (0.82 + player.Fitness / 100.0 * 0.18)
                         * (1 - FatigueOf(player) * 0.22);
            }

            var average = total / OnField.Count;
            var style = Tactics.Style switch
            {
                PlayingStyle.ForwardPower => 1.04,
                PlayingStyle.Expansive => 1.03,
                PlayingStyle.Attritional => 0.96,
                PlayingStyle.KickingGame => 0.98,
                _ => 1.0
            };

            var formation = Sheet.Formation;
            var shape = 0.96 + (formation.EdgeThreat - 0.94 + formation.MiddleThreat - 0.96) * 0.5;
            var numbers = defenceSinBinned ? 1.1 : 1.0;
            return average * style * shape * numbers;
        }

        public double DefenceRating()
        {
            if (OnField.Count == 0) return 40;
            return OnField.Average(p => p.Attributes.Tackling * 0.55 + p.Attributes.Strength * 0.2 +
                                        p.Attributes.Stamina * 0.15 + p.Attributes.Discipline * 0.1)
                   * (0.9 + Sheet.Formation.LineSpeed * 0.1)
                   * (0.92 + Tactics.LineSpeed / 500.0);
        }

        public double DisciplineRating() =>
            OnField.Count == 0 ? 55 : OnField.Average(p => p.Attributes.Discipline);

        public double HandlingRating() =>
            OnField.Count == 0 ? 55 : OnField.Average(p => p.Attributes.Handling * 0.7 + p.Attributes.Vision * 0.3);

        public double ConditionOnField() => OnField.Count == 0 ? 0.8 : 1 - FatigueOnField();

        public double FatigueOnField() => OnField.Count == 0 ? 0 : OnField.Average(FatigueOf);

        public double FatigueOf(Player player) => Runtimes.TryGetValue(player.Id, out var rt) ? rt.Fatigue : 0;

        public void FatigueRelief(double amount)
        {
            foreach (var rt in Runtimes.Values)
                rt.Fatigue = Math.Max(0, rt.Fatigue - amount);
        }

        public Player? PickCarrier(Random rng)
        {
            if (OnField.Count == 0) return null;

            // Forwards do the early grunt work; backs finish.
            var pool = OnField.Where(p =>
                p.Position is PlayerPosition.Prop or PlayerPosition.SecondRow or PlayerPosition.Lock or PlayerPosition.Hooker).ToList();
            if (pool.Count == 0 || rng.NextDouble() < 0.42) pool = OnField.ToList();

            var carrier = pool[rng.Next(pool.Count)];
            if (Runtimes.TryGetValue(carrier.Id, out var rt)) rt.Carries++;
            return carrier;
        }

        public Player? BestKicker(Random rng)
        {
            var sorted = OnField.OrderByDescending(p => p.Attributes.Kicking * 0.8 + p.Attributes.Vision * 0.2).ToList();
            return sorted.Count == 0 ? null : sorted[Math.Min(sorted.Count - 1, rng.Next(0, Math.Min(3, sorted.Count)))];
        }

        public Player? BestGoalKicker() =>
            OnField.Concat(Bench).OrderByDescending(p => p.Attributes.GoalKicking).FirstOrDefault();

        public void RecordCarry(Player? player, int metres, bool lineBreak)
        {
            if (player is null || !Runtimes.TryGetValue(player.Id, out var rt)) return;
            rt.Metres += metres;
            rt.Fatigue = Math.Min(1, rt.Fatigue + (metres / 90.0) + 0.01);
            Stats.Completions += 0;
        }

        public void RecordTry(Player? player)
        {
            if (player is null || !Runtimes.TryGetValue(player.Id, out var rt)) return;
            rt.Tries++;
            rt.Fatigue = Math.Min(1, rt.Fatigue + 0.03);
        }

        public void RecordGoal(Player? player)
        {
            if (player is null || !Runtimes.TryGetValue(player.Id, out var rt)) return;
            rt.Goals++;
        }

        public void RecordDropGoal(Player? player)
        {
            if (player is null || !Runtimes.TryGetValue(player.Id, out var rt)) return;
            rt.DropGoals++;
        }

        public void RecordError(Player? player)
        {
            if (player is null || !Runtimes.TryGetValue(player.Id, out var rt)) return;
            rt.Errors++;
        }

        public void RecordTackle(Random rng)
        {
            var tackler = OnField.Count == 0 ? null : OnField[rng.Next(0, OnField.Count)];
            if (tackler is null || !Runtimes.TryGetValue(tackler.Id, out var rt)) return;
            rt.Tackles++;
            rt.Fatigue = Math.Min(1, rt.Fatigue + 0.012);
            Stats.Tackles++;

            if (rng.NextDouble() < 0.09)
            {
                rt.MissedTackles++;
                Stats.MissedTackles++;
            }
        }

        private static double PositionalAttack(Player player, PlayerPosition role)
        {
            var a = player.Attributes;
            return role switch
            {
                PlayerPosition.Fullback => a.Vision * 0.38 + a.Pace * 0.34 + a.Passing * 0.28,
                PlayerPosition.Wing => a.Pace * 0.58 + a.Handling * 0.22 + a.Strength * 0.20,
                PlayerPosition.Centre => a.Pace * 0.40 + a.Strength * 0.34 + a.Passing * 0.26,
                PlayerPosition.FiveEighth or PlayerPosition.Halfback => a.Vision * 0.32 + a.Passing * 0.34 + a.Kicking * 0.34,
                PlayerPosition.Hooker => a.Passing * 0.46 + a.Tackling * 0.20 + a.Stamina * 0.34,
                PlayerPosition.Prop or PlayerPosition.SecondRow or PlayerPosition.Lock =>
                    a.Strength * 0.46 + a.Stamina * 0.28 + a.Handling * 0.26,
                _ => 55
            };
        }
    }
}
