namespace TimeLock;

public sealed class DesiredState
{
    public bool BlockAllNetwork { get; set; }
    public bool BlockWifi { get; set; }
    public bool BlockInternet { get; set; }
    /// <summary>בחסימת אינטרנט: האם להשאיר רשת מקומית פתוחה.</summary>
    public bool AllowLocalNetwork { get; set; } = true;
    public HashSet<string> Sites { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Processes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Extensions { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>null = אין מצב "הכול חסום חוץ מ...".</summary>
    public HashSet<string>? AllowedOnly { get; set; }
}

public static class Engine
{
    public static DesiredState Compute(IEnumerable<BlockRule> activeRules)
    {
        var d = new DesiredState();
        bool allLocalOk = true;
        foreach (var r in activeRules)
        foreach (var t in r.Targets)
        {
            switch (t.Kind)
            {
                case TargetKind.AllNetwork: d.BlockAllNetwork = true; break;
                case TargetKind.Wifi: d.BlockWifi = true; break;
                case TargetKind.Internet:
                    d.BlockInternet = true;
                    if (!r.AllowLocalNetwork) allLocalOk = false;
                    break;
                case TargetKind.Websites:
                    foreach (var v in t.Values) { var s = Catalog.NormalizeSite(v); if (s != null) d.Sites.Add(s); }
                    break;
                case TargetKind.Programs:
                    foreach (var v in t.Values) { var p = Catalog.NormalizeProcess(v); if (p != null) d.Processes.Add(p); }
                    break;
                case TargetKind.Browsers:
                    foreach (var p in Catalog.Browsers) d.Processes.Add(p);
                    foreach (var v in t.Values) { var p = Catalog.NormalizeProcess(v); if (p != null) d.Processes.Add(p); }
                    break;
                case TargetKind.MediaPlayers:
                    foreach (var p in Catalog.MediaPlayers) d.Processes.Add(p);
                    foreach (var v in t.Values) { var p = Catalog.NormalizeProcess(v); if (p != null) d.Processes.Add(p); }
                    break;
                case TargetKind.FileTypes:
                    foreach (var e in Catalog.ExpandExtensions(t.Values)) d.Extensions.Add(e);
                    break;
                case TargetKind.AllExcept:
                    var allowed = new HashSet<string>(t.Values.SelectMany(Catalog.ExpandCategory), StringComparer.OrdinalIgnoreCase);
                    if (d.AllowedOnly == null) d.AllowedOnly = allowed; else d.AllowedOnly.IntersectWith(allowed);
                    break;
            }
        }
        d.AllowLocalNetwork = allLocalOk;
        return d;
    }
}
