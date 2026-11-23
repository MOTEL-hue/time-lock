using System.Diagnostics;

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
            case "--install":
                Directory.CreateDirectory(Paths.Dir);
                // כתיבה למנהלים בלבד; משתמש רגיל יכול רק לקרוא
                Run.Exe("icacls", Paths.Dir, "/inheritance:r", "/grant:r", "*S-1-5-32-544:(OI)(CI)F",
                    "*S-1-5-18:(OI)(CI)F", "*S-1-5-32-545:(OI)(CI)RX");
                Run.Exe("sc.exe", "create", ServiceName, "binPath=", exe, "start=", "auto",
                    "DisplayName=", "TimeLock");
                Run.Exe("sc.exe", "description", ServiceName, "חסימות לפי שעות");
                Run.Exe("sc.exe", "failure", ServiceName, "reset=", "60", "actions=", "restart/5000/restart/5000/restart/5000");
                Run.Exe("sc.exe", "start", ServiceName);
                Console.WriteLine("השירות הותקן והופעל.");
                return 0;
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
            default:
                Console.WriteLine("שימוש: TimeLock.Service.exe --install | --uninstall | --reset");
                return 1;
        }
    }
}
