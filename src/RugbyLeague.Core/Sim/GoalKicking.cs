namespace RugbyLeague.Core.Sim;

/// <summary>
/// The conversion attempt model. It is deliberately deterministic: callers supply the power the
/// kicker struck the ball with, the cross wind and a random noise term, and the model says whether
/// the kick went over and how far it missed by. The 3D match animates the miss and the kicking
/// drill uses the same maths, so practice and matches feel identical.
/// </summary>
public static class GoalKicking
{
    /// <summary>Half the gap between the posts, in metres (the posts are 5.5 m apart).</summary>
    public const float PostsHalfWidth = 2.75f;

    /// <summary>Power, 0..1, needed to carry the ball the given distance.</summary>
    public static float RequiredPower(float distance) =>
        Math.Clamp(0.34f + distance / 68f, 0.34f, 0.99f);

    /// <summary>
    /// The distance the ball actually has to be struck for, once the wind helping or holding the kick
    /// is taken into account. A tailwind carries the ball further, a headwind holds it up.
    /// </summary>
    /// <param name="WindAlong">Wind along the line of the kick in km/h; positive is behind the kicker.</param>
    public static float EffectiveDistance(float distance, float windAlong) =>
        Math.Clamp(distance - windAlong / 3.6f * 1.2f, distance * 0.55f, distance * 1.45f);

    /// <summary>Metres the ball is pushed off line by a cross wind over the given distance.</summary>
    public static float LateralDrift(float windAcross, float distance) =>
        windAcross * 0.16f * (distance / 30f);

    /// <param name="Distance">Metres from the ball to the posts.</param>
    /// <param name="Power">The power meter reading at the moment of the strike, 0..1.</param>
    /// <param name="WindAcross">Cross wind in km/h; positive blows towards the far touchline.</param>
    /// <param name="GoalKicking">The kicker's goal kicking attribute, 1..99.</param>
    /// <param name="Kicking">The kicker's general kicking attribute, 1..99.</param>
    /// <param name="Noise">Random draw in -1..1.</param>
    /// <param name="WindAlong">Wind along the line of the kick in km/h; positive is behind the kicker.</param>
    public readonly record struct Attempt(float Distance, float Power, float WindAcross, int GoalKicking, int Kicking,
        float Noise, float WindAlong = 0f);

    /// <param name="Scored">True when the ball passes between the posts and over the bar.</param>
    /// <param name="LateralMiss">Metres past the posts; negative is left of the target.</param>
    /// <param name="PowerError">Strike power minus the power the distance needed.</param>
    /// <param name="Short">True when the ball lacked the legs to reach the posts.</param>
    public readonly record struct Outcome(bool Scored, float LateralMiss, float PowerError, bool Short);

    public static Outcome Evaluate(Attempt attempt)
    {
        var required = RequiredPower(EffectiveDistance(attempt.Distance, attempt.WindAlong));
        var powerError = attempt.Power - required;

        var accuracy = Math.Clamp((attempt.GoalKicking + attempt.Kicking) / 198f, 0f, 1f);
        var spread = attempt.Noise * (2.1f - accuracy * 1.5f);

        var miss = powerError * 15f
                   + LateralDrift(attempt.WindAcross, attempt.Distance)
                   + spread;

        var shortKick = attempt.Power < required * 0.62f;

        return new Outcome(!shortKick && MathF.Abs(miss) <= PostsHalfWidth, miss, powerError, shortKick);
    }
}
