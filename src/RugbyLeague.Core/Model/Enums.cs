namespace RugbyLeague.Core.Model;

/// <summary>The thirteen traditional shirt roles of a rugby league side.</summary>
public enum PlayerPosition
{
    Fullback,
    Wing,
    Centre,
    FiveEighth,
    Halfback,
    Prop,
    Hooker,
    SecondRow,
    Lock
}

/// <summary>Broad tactical approach. Each style shifts the probabilities in the match engine.</summary>
public enum PlayingStyle
{
    Balanced,
    ForwardPower,
    Expansive,
    KickingGame,
    Attritional
}

public enum TrainingFocus
{
    Balanced,
    Fitness,
    Attack,
    Defence,
    Handling,
    Kicking,
    Recovery
}

public enum InjurySeverity
{
    Knock,
    Minor,
    Moderate,
    Severe,
    SeasonEnding
}

public enum SponsorTier
{
    Main,
    Sleeve,
    Shorts,
    StadiumNaming,
    KitSupplier
}

public enum CompetitionPhase
{
    PreSeason,
    Regular,
    Finals,
    OffSeason
}

/// <summary>How the player's own fixture was decided.</summary>
public enum MatchMode
{
    Simulated,
    ThreeD
}

public enum CompetitionKind
{
    League,
    Cup
}

public enum MatchEventKind
{
    KickOff,
    Try,
    Conversion,
    ConversionMissed,
    PenaltyGoal,
    PenaltyMissed,
    DropGoal,
    DropGoalMissed,
    PenalityConceded,
    Error,
    Handover,
    FortyTwenty,
    LineBreak,
    SinBin,
    Injury,
    Interchange,
    HalfTime,
    FullTime,
    GoldenPoint,
    Commentary
}
