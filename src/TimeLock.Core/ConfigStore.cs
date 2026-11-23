using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeLock;

public static class Paths
{
    public static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TimeLock");
    public static string ConfigFile => Path.Combine(Dir, "config.json");
    public static string StateFile => Path.Combine(Dir, "state.json");
    public static string LogFile => Path.Combine(Dir, "log.txt");
}

public sealed class ConfigStore
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    readonly string _path;
    AppConfig _last = new();
    DateTime _stamp;

    public ConfigStore(string? path = null) => _path = path ?? Paths.ConfigFile;

    public AppConfig Load()
    {
        if (!File.Exists(_path)) return new AppConfig();
        return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_path), Options) ?? new AppConfig();
    }

    /// <summary>טעינה לשירות: אם הקובץ פגום או נעול, ממשיכים עם ההגדרות האחרונות התקינות.</summary>
    public AppConfig LoadSafe()
    {
        try
        {
            var stamp = File.Exists(_path) ? File.GetLastWriteTimeUtc(_path) : DateTime.MinValue;
            if (stamp != _stamp) { _last = Load(); _stamp = stamp; }
        }
        catch (Exception) { /* שומרים על ההגדרות האחרונות */ }
        return _last;
    }

    public void Save(AppConfig cfg)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(cfg, Options));
        File.Move(tmp, _path, overwrite: true);
    }
}
