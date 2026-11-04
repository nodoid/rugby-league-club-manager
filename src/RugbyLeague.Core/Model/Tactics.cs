namespace RugbyLeague.Core.Model;

public enum WeatherKind
{
    Clear,
    Overcast,
    Showers,
    HeavyRain,
    Windy,
    Cold
}

public sealed class Weather
{
    public WeatherKind Kind { get; set; } = WeatherKind.Overcast;
    public int Temperature { get; set; } = 16;

    public string Label => Kind switch
    {
        WeatherKind.Clear => "Clear",
        WeatherKind.Overcast => "Overcast",
        WeatherKind.Showers => "Showers",
        WeatherKind.HeavyRain => "Heavy rain",
        WeatherKind.Windy => "Windy",
        WeatherKind.Cold => "Cold",
        _ => Kind.ToString()
    };

    /// <summary>Extra handling error chance caused by the conditions.</summary>
    public float ErrorModifier => Kind switch
    {
        WeatherKind.Clear => -0.15f,
        WeatherKind.Overcast => 0f,
        WeatherKind.Showers => 0.12f,
        WeatherKind.HeavyRain => 0.28f,
        WeatherKind.Windy => 0.1f,
        WeatherKind.Cold => 0.05f,
        _ => 0f
    };

    /// <summary>Goal kicking difficulty modifier.</summary>
    public float KickingModifier => Kind switch
    {
        WeatherKind.Windy => 0.08f,
        WeatherKind.HeavyRain => 0.05f,
        _ => 0f
    };
}

/// <summary>Tactical sliders the coach sets before a match. All are 0..100 with 50 neutral.</summary>
public sealed class TacticalSettings
{
    public PlayingStyle Style { get; set; } = PlayingStyle.Balanced;

    /// <summary>How often the side kicks early in the tackle count.</summary>
    public int KickEarly { get; set; } = 45;

    /// <summary>Willingness to offload out of the tackle, creating broken play but also errors.</summary>
    public int Offloads { get; set; } = 50;

    /// <summary>Defensive line speed; higher pressures the attack but drains condition.</summary>
    public int LineSpeed { get; set; } = 50;

    /// <summary>How much the side targets the edges rather than the middle.</summary>
    public int Width { get; set; } = 50;

    public TacticalSettings Clone() => (TacticalSettings)MemberwiseClone();
}
