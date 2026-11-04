using System.Text.Json;
using System.Text.Json.Serialization;
using RugbyLeague.Core.Careers;

namespace RugbyLeague.Core.Persistence;

/// <summary>JSON persistence for a career. Works inside the app sandbox on Android and iOS.</summary>
public static class SaveGame
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public const string FileName = "career.json";

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RugbyLeague");

    public static string DefaultPath => Path.Combine(DefaultDirectory, FileName);

    public static string Serialize(Career career) => JsonSerializer.Serialize(career, Options);

    public static Career Deserialize(string json) =>
        JsonSerializer.Deserialize<Career>(json, Options)
        ?? throw new InvalidDataException("The save file did not contain a career.");

    public static void Save(Career career, string? path = null)
    {
        var target = path ?? DefaultPath;
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var temp = target + ".tmp";
        File.WriteAllText(temp, Serialize(career));
        File.Move(temp, target, overwrite: true);
    }

    public static Career? TryLoad(string? path = null)
    {
        var target = path ?? DefaultPath;
        if (!File.Exists(target)) return null;

        try
        {
            return Deserialize(File.ReadAllText(target));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool Exists(string? path = null) => File.Exists(path ?? DefaultPath);

    public static void Delete(string? path = null)
    {
        var target = path ?? DefaultPath;
        if (File.Exists(target)) File.Delete(target);
    }
}
