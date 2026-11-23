using System.IO.Compression;
using System.Security.Cryptography;

namespace TimeLock.Service;

/// <summary>
/// עדכון אוטומטי בטוח: מוריד, בודק חתימה ו-hash, מריץ בדיקה עצמית על הגרסה החדשה,
/// ומחליף רק כשאין שום חסימה פעילה. אם משהו נכשל, הגרסה הנוכחית ממשיכה כרגיל.
/// </summary>
public sealed class Updater
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    const long MaxPackage = 150L * 1024 * 1024;

    readonly Log _log;
    DateTime _next = DateTime.UtcNow.AddMinutes(2);
    int _busy;
    string? _stagedDir, _stagedVersion;
    bool _warned;

    static Updater() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("TimeLock-Updater");

    public Updater(Log log) => _log = log;

    public static string InstallDir => AppContext.BaseDirectory;

    /// <summary>ניקוי קבצים ישנים משדרוג קודם.</summary>
    public static void CleanupOld()
    {
        try
        {
            foreach (var f in Directory.GetFiles(InstallDir, "*.old")) { try { File.Delete(f); } catch (Exception) { } }
            var upd = Path.Combine(Paths.Dir, "update");
            if (Directory.Exists(upd)) Directory.Delete(upd, true);
        }
        catch (Exception) { }
    }

    /// <summary>נקרא כל כמה שניות. מחזיר true אם הוחלף והשירות צריך להיכבות ולעלות מחדש.</summary>
    public bool Poll(bool idle)
    {
        if (_stagedDir != null) return idle && Apply();
        if (DateTime.UtcNow >= _next && Interlocked.CompareExchange(ref _busy, 1, 0) == 0)
        {
            _next = DateTime.UtcNow.AddHours(6);
            _ = Task.Run(async () =>
            {
                try { await CheckAsync(); }
                catch (Exception e)
                {
                    _log.Write("בדיקת עדכון נכשלה: " + e.Message);
                    _next = DateTime.UtcNow.AddHours(1);
                }
                finally { Volatile.Write(ref _busy, 0); }
            });
        }
        return false;
    }

    async Task CheckAsync()
    {
        if (string.IsNullOrWhiteSpace(UpdateService.PublicKeyPem))
        {
            if (!_warned) { _log.Write("עדכון אוטומטי כבוי: אין מפתח חתימה"); _warned = true; }
            return;
        }
        var manifest = await Http.GetByteArrayAsync(UpdateService.LatestBase + "update.json");
        var sig = await Http.GetByteArrayAsync(UpdateService.LatestBase + "update.json.sig");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(UpdateService.PublicKeyPem);
        var m = UpdateService.ParseAndVerify(manifest, sig, rsa);
        if (m == null) { _log.Write("עדכון נדחה: חתימה או כתובת לא תקינות"); return; }
        if (!UpdateService.IsNewer(m.Version, AppInfo.Version)) return;

        var dir = Path.Combine(Paths.Dir, "update");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        var zip = Path.Combine(dir, "pkg.zip");
        using (var resp = await Http.GetAsync(m.Url, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            if (resp.Content.Headers.ContentLength > MaxPackage) throw new InvalidOperationException("החבילה גדולה מדי");
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(zip);
            var buf = new byte[81920]; long total = 0; int n;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                total += n;
                if (total > MaxPackage) throw new InvalidOperationException("החבילה גדולה מדי");
                await dst.WriteAsync(buf.AsMemory(0, n));
            }
        }
        if (!UpdateService.Sha256Matches(zip, m.Sha256)) { _log.Write("עדכון נדחה: hash לא תואם"); return; }

        var stage = Path.Combine(dir, "stage");
        ZipFile.ExtractToDirectory(zip, stage);
        var newService = Path.Combine(stage, "TimeLock.Service.exe");
        if (!File.Exists(newService)) { _log.Write("עדכון נדחה: חסר קובץ שירות"); return; }
        // הגרסה החדשה חייבת לעלות ולדווח על הגרסה הנכונה, לפני שמחליפים משהו
        if (Run.Exe(newService, "--selftest", m.Version) != 0) { _log.Write("עדכון נדחה: בדיקה עצמית נכשלה"); return; }

        _stagedDir = stage; _stagedVersion = m.Version;
        _log.Write($"גרסה {m.Version} הורדה ונבדקה, תוחלף כשלא תהיה חסימה פעילה");
    }

    bool Apply()
    {
        var swapped = new List<string>();
        try
        {
            foreach (var name in new[] { "TimeLock.Service.exe", "TimeLock.exe" })
            {
                var neu = Path.Combine(_stagedDir!, name);
                if (!File.Exists(neu)) continue;
                var cur = Path.Combine(InstallDir, name);
                var old = cur + ".old";
                if (File.Exists(old)) File.Delete(old);
                // קובץ שרץ אפשר לשנות לו שם אבל לא לדרוס אותו
                if (File.Exists(cur)) File.Move(cur, old);
                swapped.Add(name);
                File.Copy(neu, cur, true);
            }
        }
        catch (Exception e)
        {
            _log.Write("החלפת הגרסה נכשלה, חוזרים לגרסה הקודמת: " + e.Message);
            foreach (var name in swapped)
            {
                var cur = Path.Combine(InstallDir, name);
                try { if (File.Exists(cur + ".old")) File.Move(cur + ".old", cur, true); } catch (Exception) { }
            }
            _stagedDir = null;
            _next = DateTime.UtcNow.AddHours(6);
            return false;
        }
        _log.Write($"הגרסה הוחלפה ל-{_stagedVersion}, השירות עולה מחדש");
        // גיבוי למקרה שהגדרות ההפעלה מחדש של Windows לא קיימות
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe",
                "/c timeout /t 8 /nobreak >nul & sc start " + Installer.ServiceName)
            { CreateNoWindow = true, UseShellExecute = false });
        }
        catch (Exception) { }
        return true;
    }
}
