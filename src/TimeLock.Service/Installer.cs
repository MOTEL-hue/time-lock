namespace TimeLock.Service;

public static class Installer
{
    public const string ServiceName = "TimeLock";

    public static int Handle(string[] args)
    {
        var exe = Environment.ProcessPath!;
        var log = new Log(Paths.LogFile);
        switch (args[0].ToLowerInvariant())
        {
            case "--install": return Install(exe);
            case "--uninstall":
                Run.Exe("sc.exe", "stop", ServiceName);
                Run.Exe("sc.exe", "delete", ServiceName);
                Recovery.CleanAll(log);
                Console.WriteLine("השירות הוסר והכול הוחזר לרגיל.");
                return 0;
            case "--reset":
                Recovery.CleanAll(log);
                Console.WriteLine("הכול הוחזר למצב רגיל (כרטיסי רשת, חומת אש, אתרים).");
                return 0;
            case "--version":
                Console.WriteLine(AppInfo.Version);
                return 0;
            case "--selftest":
                // בדיקה שהקובץ עולה ומדווח על הגרסה הצפויה. משמשת את העדכון האוטומטי.
                return args.Length > 1 && args[1] == AppInfo.Version ? 0 : 3;
            default:
                Console.WriteLine("שימוש: TimeLock.Service.exe --install | --uninstall | --reset | --version");
                return 1;
        }
    }

    /// <summary>התקנה (וגם שדרוג ידני): מעתיק ל-Program Files, ומגדיר שירות שעולה עם Windows.</summary>
    static int Install(string exe)
    {
        try
        {
            var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TimeLock");
            var srcDir = Path.GetDirectoryName(exe)!;
            Run.Exe("sc.exe", "stop", ServiceName);
            Thread.Sleep(3000);
            Directory.CreateDirectory(target);
            var svcExe = Path.Combine(target, "TimeLock.Service.exe");
            if (!string.Equals(Path.GetFullPath(srcDir).TrimEnd('\\'), Path.GetFullPath(target).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(exe, svcExe, true);
                var app = Path.Combine(srcDir, "TimeLock.exe");
                if (File.Exists(app)) File.Copy(app, Path.Combine(target, "TimeLock.exe"), true);
            }
            Directory.CreateDirectory(Paths.Dir);
            // כתיבה למנהלים בלבד; משתמש רגיל יכול רק לקרוא
            Run.Exe("icacls", Paths.Dir, "/inheritance:r", "/grant:r", "*S-1-5-32-544:(OI)(CI)F",
                "*S-1-5-18:(OI)(CI)F", "*S-1-5-32-545:(OI)(CI)RX");
            if (Run.Exe("sc.exe", "query", ServiceName) == 0)
                Run.Exe("sc.exe", "config", ServiceName, "binPath=", svcExe, "start=", "auto");
            else
                Run.Exe("sc.exe", "create", ServiceName, "binPath=", svcExe, "start=", "auto", "DisplayName=", "TimeLock");
            Run.Exe("sc.exe", "description", ServiceName, "חסימות לפי שעות");
            Run.Exe("sc.exe", "failure", ServiceName, "reset=", "60", "actions=", "restart/5000/restart/5000/restart/5000");
            Run.Exe("sc.exe", "start", ServiceName);

            var appPath = Path.Combine(target, "TimeLock.exe");
            if (File.Exists(appPath))
                Run.Exe("powershell", "-NoProfile", "-Command",
                    "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('C:\\Users\\Public\\Desktop\\TimeLock.lnk');" +
                    $"$s.TargetPath='{appPath}';$s.Save()");
            Console.WriteLine($"הותקן ב-{target} והופעל (גרסה {AppInfo.Version}). מכאן התוכנה מתעדכנת לבד.");
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine("ההתקנה נכשלה: " + e.Message + "\nסגור את חלון TimeLock אם הוא פתוח, והרץ שוב כמנהל.");
            return 2;
        }
    }
}
