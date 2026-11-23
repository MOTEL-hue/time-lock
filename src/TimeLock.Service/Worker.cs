namespace TimeLock.Service;

public sealed class Worker : BackgroundService
{
    readonly Log _log = new(Paths.LogFile);
    readonly ConfigStore _store = new();
    readonly NetworkEnforcer _net;
    readonly HostsEnforcer _hosts = new();
    readonly ProcessEnforcer _procs;
    /// <summary>חסימות קשות שפעילות כרגע, נשמרות בזיכרון בלבד: עריכת הקובץ לא משנה אותן עד סוף השעות.</summary>
    readonly Dictionary<Guid, BlockRule> _locked = new();

    public Worker()
    {
        _net = new NetworkEnforcer(_log);
        _procs = new ProcessEnforcer(_log);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.Write("השירות התחיל");
        Recovery.CleanAll(_log);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(ct)) Tick();
        }
        catch (OperationCanceledException) { }
        finally
        {
            Recovery.CleanAll(_log);
            _log.Write("השירות נעצר, הכול הוחזר לרגיל");
        }
    }

    void Tick()
    {
        var cfg = _store.LoadSafe();
        if (cfg.FirstRunUtc == null)
        {
            cfg.FirstRunUtc = DateTime.UtcNow;
            try { _store.Save(cfg); } catch (Exception e) { _log.Write("שמירה נכשלה: " + e.Message); }
        }
        var lic = LicenseService.Evaluate(cfg, DateTime.UtcNow);
        var now = DateTime.Now;
        var effective = new Dictionary<Guid, BlockRule>();

        if (lic.AllowEnforcement)
        {
            foreach (var r in cfg.Rules.Where(r => Schedule.IsActive(r, now)))
            {
                effective[r.Id] = r;
                if (r.Strictness == Strictness.Hard) _locked.TryAdd(r.Id, r);
            }
            foreach (var snap in _locked.Values.ToList())
            {
                if (Schedule.IsActive(snap, now)) effective[snap.Id] = snap;
                else _locked.Remove(snap.Id);
            }
        }
        else _locked.Clear();

        var d = Engine.Compute(effective.Values);
        Safe("רשת", () => _net.Apply(d));
        Safe("אתרים", () => _hosts.Apply(d.Sites));
        Safe("תוכנות", () => _procs.Enforce(d));
    }

    void Safe(string what, Action a)
    {
        try { a(); }
        catch (Exception e) { _log.Write($"שגיאה ב{what}: {e.Message}"); }
    }
}
