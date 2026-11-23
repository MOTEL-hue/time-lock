using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TimeLock.Service;

/// <summary>כרטיסי רשת וחומת אש. כל מה שנשנה נרשם בקובץ שחזור, כדי שאפשר יהיה להחזיר הכול.</summary>
public sealed class NetworkEnforcer
{
    const string FwRule = "TimeLock-Internet";
    const string PublicV4 =
        "1.0.0.0-9.255.255.255,11.0.0.0-126.255.255.255,128.0.0.0-169.253.255.255,169.255.0.0-172.15.255.255," +
        "172.32.0.0-191.255.255.255,192.0.0.0-192.167.255.255,192.169.0.0-223.255.255.255,2000::/3";

    readonly Log _log;
    readonly HashSet<string> _disabled;
    string? _fwKey;

    public NetworkEnforcer(Log log)
    {
        _log = log;
        _disabled = Recovery.LoadDisabled();
    }

    public void Apply(DesiredState d)
    {
        ApplyAdapters(d);
        ApplyFirewall(d);
    }

    static bool IsPhysical(NetworkInterface n)
    {
        var t = n.NetworkInterfaceType;
        if (t is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) return false;
        if (t is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211 or
            NetworkInterfaceType.GigabitEthernet or
            NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2)) return false;
        var s = n.Name + " " + n.Description;
        return !Regex.IsMatch(s, "Virtual|vEthernet|VMware|VirtualBox|Bluetooth|Loopback|Hyper-V|TAP-|WireGuard|Npcap",
            RegexOptions.IgnoreCase);
    }

    void ApplyAdapters(DesiredState d)
    {
        var all = NetworkInterface.GetAllNetworkInterfaces().Where(IsPhysical).ToList();
        var want = new HashSet<string>();
        foreach (var n in all)
        {
            if (d.BlockAllNetwork || (d.BlockWifi && n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                want.Add(n.Name);
        }
        bool changed = false;
        foreach (var n in all)
        {
            if (want.Contains(n.Name) && !_disabled.Contains(n.Name) && n.OperationalStatus == OperationalStatus.Up)
            {
                if (Run.Exe("netsh", "interface", "set", "interface", $"name={n.Name}", "admin=disabled") == 0)
                {
                    _disabled.Add(n.Name); changed = true;
                    _log.Write($"כובה כרטיס רשת: {n.Name}");
                }
            }
        }
        foreach (var name in _disabled.ToList())
        {
            if (want.Contains(name)) continue;
            Run.Exe("netsh", "interface", "set", "interface", $"name={name}", "admin=enabled");
            _disabled.Remove(name); changed = true;
            _log.Write($"הופעל כרטיס רשת: {name}");
        }
        if (changed) Recovery.SaveDisabled(_disabled);
    }

    void ApplyFirewall(DesiredState d)
    {
        var key = d.BlockInternet ? (d.AllowLocalNetwork ? "public" : "all") : "none";
        if (key == _fwKey) return;
        Run.Exe("netsh", "advfirewall", "firewall", "delete", "rule", $"name={FwRule}");
        if (key != "none")
        {
            var remote = key == "public" ? PublicV4 : "any";
            Run.Exe("netsh", "advfirewall", "firewall", "add", "rule", $"name={FwRule}", "dir=out",
                "action=block", "enable=yes", "profile=any", $"remoteip={remote}");
            _log.Write($"נחסם אינטרנט ({key})");
        }
        else _log.Write("חסימת האינטרנט הוסרה");
        _fwKey = key;
    }
}

public sealed class HostsEnforcer
{
    const string Begin = "# TimeLock begin";
    const string End = "# TimeLock end";
    static string HostsPath => Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");
    string _last = "";

    public void Apply(IEnumerable<string> sites) => Write(sites.OrderBy(s => s).ToList());

    void Write(List<string> sites)
    {
        var key = string.Join(",", sites);
        if (key == _last) return;
        var lines = Strip(File.ReadAllLines(HostsPath));
        if (sites.Count > 0)
        {
            lines.Add(Begin);
            foreach (var s in sites)
                foreach (var h in new[] { s, "www." + s })
                {
                    lines.Add($"0.0.0.0 {h}");
                    lines.Add($":: {h}");
                }
            lines.Add(End);
        }
        File.WriteAllLines(HostsPath, lines, new UTF8Encoding(false));
        Run.Exe("ipconfig", "/flushdns");
        _last = key;
    }

    static List<string> Strip(string[] lines)
    {
        var res = new List<string>();
        bool inside = false;
        foreach (var l in lines)
        {
            if (l.Trim() == Begin) { inside = true; continue; }
            if (l.Trim() == End) { inside = false; continue; }
            if (!inside) res.Add(l);
        }
        return res;
    }

    public static void Clean()
    {
        var lines = File.ReadAllLines(HostsPath);
        if (lines.Any(l => l.Trim() == Begin))
            File.WriteAllLines(HostsPath, Strip(lines), new UTF8Encoding(false));
    }
}

public sealed class ProcessEnforcer
{
    readonly Log _log;
    public ProcessEnforcer(Log log) => _log = log;

    public void Enforce(DesiredState d)
    {
        if (d.Processes.Count == 0 && d.Extensions.Count == 0 && d.AllowedOnly == null) return;
        int self = Environment.ProcessId;

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id == self || p.SessionId == 0) continue;
                var name = p.ProcessName.ToLowerInvariant();
                if (Catalog.Protected.Contains(name)) continue;
                bool kill = d.Processes.Contains(name);
                if (!kill && d.AllowedOnly != null && p.MainWindowHandle != IntPtr.Zero &&
                    !d.AllowedOnly.Contains(name)) kill = true;
                if (kill) { p.Kill(true); _log.Write($"נסגרה תוכנה: {name}"); }
            }
            catch (Exception) { /* התהליך נסגר מעצמו או שאין הרשאה */ }
            finally { p.Dispose(); }
        }

        if (d.Extensions.Count > 0) KillByCommandLine(d.Extensions, self);
    }

    void KillByCommandLine(HashSet<string> exts, int self)
    {
        var rx = new Regex(@"\.(" + string.Join("|", exts.Select(Regex.Escape)) + @")(""|'|\s|$)", RegexOptions.IgnoreCase);
        using var s = new ManagementObjectSearcher(
            "SELECT ProcessId, Name, CommandLine FROM Win32_Process WHERE SessionId <> 0");
        foreach (ManagementObject mo in s.Get())
        {
            try
            {
                var cmd = mo["CommandLine"] as string;
                var pid = Convert.ToInt32(mo["ProcessId"]);
                var name = ((mo["Name"] as string) ?? "").ToLowerInvariant();
                if (pid == self || cmd == null || !rx.IsMatch(cmd)) continue;
                if (name.EndsWith(".exe")) name = name[..^4];
                if (Catalog.Protected.Contains(name)) continue;
                Process.GetProcessById(pid).Kill(true);
                _log.Write($"נסגרה תוכנה שפתחה קובץ חסום: {name}");
            }
            catch (Exception) { }
            finally { mo.Dispose(); }
        }
    }
}

/// <summary>החזרת הכול למצב רגיל. רץ בכל הפעלה של השירות וגם בעצירה.</summary>
public static class Recovery
{
    public static HashSet<string> LoadDisabled()
    {
        try
        {
            if (File.Exists(Paths.StateFile))
                return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(Paths.StateFile)) ?? new();
        }
        catch (Exception) { }
        return new();
    }

    public static void SaveDisabled(HashSet<string> names)
    {
        try
        {
            Directory.CreateDirectory(Paths.Dir);
            if (names.Count == 0) { if (File.Exists(Paths.StateFile)) File.Delete(Paths.StateFile); }
            else File.WriteAllText(Paths.StateFile, JsonSerializer.Serialize(names));
        }
        catch (Exception) { }
    }

    public static void CleanAll(Log log)
    {
        try { Run.Exe("netsh", "advfirewall", "firewall", "delete", "rule", "name=TimeLock-Internet"); }
        catch (Exception e) { log.Write("ניקוי חומת אש נכשל: " + e.Message); }
        try
        {
            foreach (var name in LoadDisabled())
                Run.Exe("netsh", "interface", "set", "interface", $"name={name}", "admin=enabled");
            SaveDisabled(new());
        }
        catch (Exception e) { log.Write("הפעלת כרטיסי רשת נכשלה: " + e.Message); }
        try { HostsEnforcer.Clean(); }
        catch (Exception e) { log.Write("ניקוי קובץ hosts נכשל: " + e.Message); }
    }
}
