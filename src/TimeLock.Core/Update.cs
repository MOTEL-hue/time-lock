using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TimeLock;

public sealed record UpdateManifest(string Version, string Sha256, string Url);

public static class AppInfo
{
    public static string Version =>
        (Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0)).ToString(3);
}

public static class UpdateService
{
    /// <summary>הקובץ מגיע מ-GitHub, ורק מהמאגר הזה.</summary>
    public const string UrlPrefix = "https://github.com/MOTEL-hue/time-lock/releases/download/";

    /// <summary>התוכנה שואלת רק את האתר איזו גרסה זמינה. גרסה יוצאת ללקוחות רק אחרי שבעל האתר פרסם אותה שם.</summary>
    public const string ManifestUrl = "https://kolhodaot.co.il/timelock/update.json";

    /// <summary>מחזיר את פרטי העדכון רק אם הם תקינים והכתובת מהמאגר שלנו.</summary>
    public static UpdateManifest? Parse(byte[] manifest)
    {
        try
        {
            if (manifest.Length > 4096) return null;
            var m = JsonSerializer.Deserialize<UpdateManifest>(manifest,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (m == null || !System.Version.TryParse(m.Version, out _)) return null;
            if (!Regex.IsMatch(m.Sha256 ?? "", "^[0-9a-fA-F]{64}$")) return null;
            if (m.Url == null || !m.Url.StartsWith(UrlPrefix, StringComparison.OrdinalIgnoreCase) ||
                !m.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return null;
            return m;
        }
        catch (Exception) { return null; }
    }

    public static bool IsNewer(string offered, string current) =>
        System.Version.TryParse(offered, out var o) && System.Version.TryParse(current, out var c) && o > c;

    public static bool Sha256Matches(string file, string hex)
    {
        using var fs = File.OpenRead(file);
        var actual = Convert.ToHexString(SHA256.HashData(fs));
        return actual.Equals(hex, StringComparison.OrdinalIgnoreCase);
    }
}
