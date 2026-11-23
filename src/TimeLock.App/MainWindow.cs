using System.Security.Cryptography;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;

namespace TimeLock.App;

public sealed class MainWindow : Window
{
    static readonly string[] DayShort = { "א", "ב", "ג", "ד", "ה", "ו", "ש" };
    readonly ConfigStore _store = new();
    AppConfig _cfg = new();
    readonly ListBox _list = new() { Margin = new Thickness(0, 8, 0, 8) };
    readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock _lic = new();
    List<BlockRule> _shown = new();

    public MainWindow()
    {
        Title = "TimeLock - חסימות לפי שעות"; Width = 780; Height = 560; FontSize = 14;
        FlowDirection = FlowDirection.RightToLeft; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Loaded += (_, _) =>
        {
            try { _cfg = _store.Load(); }
            catch (Exception ex) { Dialogs.Error(this, "קובץ ההגדרות פגום: " + ex.Message); Close(); return; }
            if (!Authenticate()) { Close(); return; }
            BuildUi();
            Refresh();
        };
    }

    bool Authenticate()
    {
        if (_cfg.PasswordHash == null)
        {
            var p1 = Dialogs.Ask(this, "הגדרת סיסמה", "זו הפעלה ראשונה. בחר סיסמה לניהול החסימות (לפחות 4 תווים):");
            if (p1 == null || p1.Length < 4) return false;
            var p2 = Dialogs.Ask(this, "הגדרת סיסמה", "הקלד את הסיסמה שוב:");
            if (p2 != p1) { Dialogs.Error(this, "הסיסמאות לא זהות."); return false; }
            var code = RandomNumberGenerator.GetInt32(10_000_000, 100_000_000).ToString();
            _cfg.PasswordHash = PasswordHasher.Hash(p1);
            _cfg.EmergencyCodeHash = PasswordHasher.Hash(code);
            _cfg.FirstRunUtc ??= DateTime.UtcNow;
            Save();
            Dialogs.Info(this, "קוד החירום שלך: " + code + "\n\nרשום אותו ושמור במקום בטוח. הוא יוצג רק עכשיו, והוא נדרש כדי לבטל חסימה בינונית.");
            return true;
        }
        for (int i = 0; i < 3; i++)
        {
            var p = Dialogs.Ask(this, "כניסה", "הקלד סיסמה:");
            if (p == null) return false;
            if (PasswordHasher.Verify(p, _cfg.PasswordHash)) return true;
            Dialogs.Error(this, "סיסמה שגויה.");
        }
        return false;
    }

    void BuildUi()
    {
        var root = new DockPanel { Margin = new Thickness(14) };
        var top = new StackPanel();
        top.Children.Add(_status);
        top.Children.Add(_lic);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        var buttons = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        Button B(string t, Action a)
        {
            var b = new Button { Content = t, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(0, 0, 8, 0) };
            b.Click += (_, _) =>
            {
                try { a(); }
                catch (UnauthorizedAccessException) { Dialogs.Error(this, "אין הרשאה לשמור. הפעל את התוכנה כמנהל."); }
                catch (Exception ex) { Dialogs.Error(this, "שגיאה: " + ex.Message); }
            };
            return b;
        }
        buttons.Children.Add(B("חסימה חדשה", NewRule));
        buttons.Children.Add(B("עריכה", EditRule));
        buttons.Children.Add(B("הפעלה / כיבוי", ToggleRule));
        buttons.Children.Add(B("מחיקה", DeleteRule));
        buttons.Children.Add(B("שינוי סיסמה", ChangePassword));
        buttons.Children.Add(B("רישיון", LicenseDialog));
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        root.Children.Add(_list);
        Content = root;
    }

    void Save() => _store.Save(_cfg);

    void Refresh()
    {
        _shown = _cfg.Rules.ToList();
        _list.Items.Clear();
        var now = DateTime.Now;
        foreach (var r in _shown) _list.Items.Add(Describe(r, now));
        if (_shown.Count == 0) _list.Items.Add("אין חסימות עדיין. לחץ על \"חסימה חדשה\".");
        _status.Text = ServiceText();
        _status.Foreground = _status.Text.StartsWith("השירות פועל") ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.Firebrick;
        var lic = LicenseService.Evaluate(_cfg, DateTime.UtcNow);
        _lic.Text = lic.Status switch
        {
            LicenseStatus.Licensed => "רישיון: פעיל" + (lic.Holder != null ? " (" + lic.Holder + ")" : ""),
            LicenseStatus.Trial => $"תקופת ניסיון: נשארו {lic.TrialDaysLeft} ימים",
            _ => "תקופת הניסיון הסתיימה. החסימות כבויות עד הזנת רישיון."
        };
    }

    static string ServiceText()
    {
        try
        {
            using var sc = new ServiceController("TimeLock");
            return sc.Status == ServiceControllerStatus.Running
                ? "השירות פועל. החסימות נאכפות לפי ההגדרות."
                : "השירות מותקן אבל לא פועל, ולכן שום חסימה לא נאכפת!";
        }
        catch (InvalidOperationException) { return "השירות לא מותקן, ולכן שום חסימה לא נאכפת. הרץ: TimeLock.Service.exe --install"; }
    }

    static string Describe(BlockRule r, DateTime now)
    {
        var days = string.Join("", r.Days.OrderBy(d => (int)d).Select(d => DayShort[(int)d]));
        var time = r.StartMinute == r.EndMinute ? "כל היום" : $"{r.StartMinute / 60:00}:{r.StartMinute % 60:00}-{r.EndMinute / 60:00}:{r.EndMinute % 60:00}";
        var strict = r.Strictness switch { Strictness.Soft => "קלה", Strictness.Code => "בינונית", _ => "קשה" };
        var what = string.Join(", ", r.Targets.Select(t => t.Kind switch
        {
            TargetKind.AllNetwork => "כל הרשת", TargetKind.Wifi => "WiFi", TargetKind.Internet => "אינטרנט",
            TargetKind.Websites => "אתרים", TargetKind.Programs => "תוכנות", TargetKind.Browsers => "דפדפנים",
            TargetKind.MediaPlayers => "נגנים", TargetKind.FileTypes => "קבצי מדיה", _ => "הכול חוץ מ..."
        }));
        var active = Schedule.IsActive(r, now) ? "  ◄ פעילה עכשיו" : "";
        return $"{(r.Enabled ? "✔" : "✖")}  {r.Name}  |  ימים {days}  {time}  |  חומרה: {strict}  |  {what}{active}";
    }

    BlockRule? Selected()
    {
        var i = _list.SelectedIndex;
        if (i < 0 || i >= _shown.Count) { Dialogs.Info(this, "בחר חסימה מהרשימה."); return null; }
        return _shown[i];
    }

    /// <summary>בדיקה לפני שינוי חסימה שפעילה כרגע, לפי החומרה שלה.</summary>
    bool Guard(BlockRule r)
    {
        if (!Schedule.IsActive(r, DateTime.Now)) return true;
        switch (r.Strictness)
        {
            case Strictness.Hard:
                var left = Schedule.Remaining(r, DateTime.Now);
                Dialogs.Error(this, $"זו חסימה קשה והיא פעילה. אפשר יהיה לשנות אותה בעוד {(int)left.TotalHours:0}:{left.Minutes:00} שעות.");
                return false;
            case Strictness.Code:
                var c = Dialogs.Ask(this, "קוד חירום", "החסימה פעילה. הקלד את קוד החירום:");
                if (c != null && PasswordHasher.Verify(c, _cfg.EmergencyCodeHash)) return true;
                Dialogs.Error(this, "קוד חירום שגוי."); return false;
            default:
                var p = Dialogs.Ask(this, "אימות", "החסימה פעילה. הקלד שוב את הסיסמה:");
                if (p != null && PasswordHasher.Verify(p, _cfg.PasswordHash)) return true;
                Dialogs.Error(this, "סיסמה שגויה."); return false;
        }
    }

    void NewRule()
    {
        var ed = new RuleEditor(this, null);
        if (ed.ShowDialog() == true && ed.Result != null) { _cfg.Rules.Add(ed.Result); Save(); Refresh(); }
    }

    void EditRule()
    {
        var r = Selected(); if (r == null || !Guard(r)) return;
        var ed = new RuleEditor(this, r);
        if (ed.ShowDialog() != true || ed.Result == null) return;
        _cfg.Rules[_cfg.Rules.IndexOf(r)] = ed.Result; Save(); Refresh();
    }

    void ToggleRule()
    {
        var r = Selected(); if (r == null) return;
        if (r.Enabled && !Guard(r)) return;
        r.Enabled = !r.Enabled; Save(); Refresh();
    }

    void DeleteRule()
    {
        var r = Selected(); if (r == null || !Guard(r)) return;
        if (!Dialogs.Confirm(this, "למחוק את החסימה \"" + r.Name + "\"?")) return;
        _cfg.Rules.Remove(r); Save(); Refresh();
    }

    void ChangePassword()
    {
        var old = Dialogs.Ask(this, "שינוי סיסמה", "הקלד את הסיסמה הנוכחית:");
        if (old == null || !PasswordHasher.Verify(old, _cfg.PasswordHash)) { Dialogs.Error(this, "סיסמה שגויה."); return; }
        var p1 = Dialogs.Ask(this, "שינוי סיסמה", "סיסמה חדשה (לפחות 4 תווים):");
        if (p1 == null || p1.Length < 4) return;
        if (Dialogs.Ask(this, "שינוי סיסמה", "הקלד שוב:") != p1) { Dialogs.Error(this, "הסיסמאות לא זהות."); return; }
        _cfg.PasswordHash = PasswordHasher.Hash(p1); Save();
        Dialogs.Info(this, "הסיסמה שונתה.");
    }

    void LicenseDialog()
    {
        var key = Dialogs.Ask(this, "רישיון", "הדבק כאן את מפתח הרישיון (ריק = ביטול):", secret: false);
        if (string.IsNullOrWhiteSpace(key)) return;
        _cfg.LicenseKey = key.Trim(); Save(); Refresh();
        Dialogs.Info(this, _lic.Text);
    }
}
