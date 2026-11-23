using System.Text.RegularExpressions;

namespace TimeLock;

public static class Catalog
{
    public static readonly string[] Browsers =
    {
        "chrome", "msedge", "firefox", "brave", "opera", "opera_gx", "vivaldi", "iexplore",
        "waterfox", "chromium", "browser", "librewolf", "tor", "palemoon"
    };

    public static readonly string[] MediaPlayers =
    {
        "vlc", "wmplayer", "mpc-hc", "mpc-hc64", "mpc-be", "mpc-be64", "potplayer", "potplayermini",
        "potplayermini64", "kmplayer", "mpv", "gom", "foobar2000", "winamp", "aimp", "spotify", "itunes"
    };

    public static readonly string[] VideoExtensions =
        { "mp4", "mkv", "avi", "mov", "wmv", "flv", "webm", "m4v", "mpg", "mpeg", "3gp", "ts", "vob" };

    public static readonly string[] AudioExtensions = { "mp3", "wav", "flac", "aac", "m4a", "ogg", "wma" };

    /// <summary>תוכנות שלעולם לא נסגרות, כדי שהמחשב לא ייתקע.</summary>
    public static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "dwm", "csrss", "winlogon", "wininit", "services", "lsass", "svchost", "smss",
        "system", "idle", "registry", "timelock", "timelock.service", "timelock.app", "taskmgr",
        "conhost", "ctfmon", "sihost", "runtimebroker", "shellexperiencehost", "searchhost",
        "startmenuexperiencehost", "textinputhost", "applicationframehost", "fontdrvhost", "logonui",
        "lockapp", "securityhealthsystray", "securityhealthservice", "msmpeng", "nissrv", "wmiprvse",
        "dllhost", "taskhostw", "backgroundtaskhost", "systemsettings", "widgets", "widgetservice",
        "userinit", "audiodg", "spoolsv", "searchindexer", "searchprotocolhost", "msiexec", "rundll32"
    };

    public static IEnumerable<string> ExpandCategory(string token)
    {
        switch (token.ToLowerInvariant())
        {
            case "@word": return new[] { "winword" };
            case "@excel": return new[] { "excel" };
            case "@powerpoint": return new[] { "powerpnt" };
            case "@outlook": return new[] { "outlook" };
            case "@pdf": return new[] { "acrord32", "acrobat", "sumatrapdf", "foxitreader", "foxitpdfreader" };
            case "@notepad": return new[] { "notepad" };
            case "@browsers": return Browsers;
            default:
                var n = NormalizeProcess(token);
                return n == null ? Array.Empty<string>() : new[] { n };
        }
    }

    public static IEnumerable<string> ExpandExtensions(IEnumerable<string> values)
    {
        foreach (var v in values)
        {
            var t = v.Trim().ToLowerInvariant();
            if (t == "@video") foreach (var e in VideoExtensions) yield return e;
            else if (t == "@audio") foreach (var e in AudioExtensions) yield return e;
            else
            {
                t = t.TrimStart('.', '*');
                if (Regex.IsMatch(t, "^[a-z0-9]{1,8}$")) yield return t;
            }
        }
    }

    public static string? NormalizeProcess(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim().ToLowerInvariant();
        if (t.EndsWith(".exe")) t = t[..^4];
        return t.Length == 0 || t.IndexOfAny(new[] { '\\', '/', ':' }) >= 0 ? null : t;
    }

    public static string? NormalizeSite(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim().ToLowerInvariant();
        var i = t.IndexOf("://", StringComparison.Ordinal);
        if (i >= 0) t = t[(i + 3)..];
        t = t.Split('/', '?', '#')[0];
        t = t.Split(':')[0];
        if (t.StartsWith("www.")) t = t[4..];
        return Regex.IsMatch(t, @"^([a-z0-9-]+\.)+[a-z0-9-]{2,}$") ? t : null;
    }
}
