using System.Windows;
using System.Windows.Controls;

namespace TimeLock.App;

/// <summary>חלון עריכת חסימה אחת.</summary>
public sealed class RuleEditor : Window
{
    static readonly (DayOfWeek Day, string Name)[] DayNames =
    {
        (DayOfWeek.Sunday, "ראשון"), (DayOfWeek.Monday, "שני"), (DayOfWeek.Tuesday, "שלישי"),
        (DayOfWeek.Wednesday, "רביעי"), (DayOfWeek.Thursday, "חמישי"), (DayOfWeek.Friday, "שישי"),
        (DayOfWeek.Saturday, "שבת")
    };

    readonly Guid _id;
    readonly TextBox _name = new(), _start = new() { Width = 70 }, _end = new() { Width = 70 };
    readonly List<(DayOfWeek Day, CheckBox Box)> _days = new();
    readonly ComboBox _strict = new();
    readonly CheckBox _local = new() { Content = "להשאיר רשת מקומית ומדפסת פתוחות (בחסימת אינטרנט)", IsChecked = true };
    readonly CheckBox _all = new() { Content = "כיבוי כל כרטיסי הרשת (גם רשת מקומית)" };
    readonly CheckBox _wifi = new() { Content = "כיבוי ה-WiFi בלבד" };
    readonly CheckBox _net = new() { Content = "חסימת אינטרנט" };
    readonly CheckBox _browsers = new() { Content = "כל הדפדפנים" };
    readonly CheckBox _players = new() { Content = "נגני מדיה (וידאו ומוזיקה)" };
    readonly CheckBox _video = new() { Content = "פתיחת קבצי וידאו מכל סוג" };
    readonly CheckBox _audio = new() { Content = "פתיחת קבצי שמע מכל סוג" };
    readonly TextBox _sites = new() { AcceptsReturn = true, Height = 54, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    readonly TextBox _progs = new() { AcceptsReturn = true, Height = 54, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    readonly CheckBox _except = new() { Content = "הכול חסום חוץ מהתוכנות שאסמן כאן:" };
    readonly (string Token, CheckBox Box)[] _exceptApps =
    {
        ("@Word", new CheckBox { Content = "וורד" }), ("@Excel", new CheckBox { Content = "אקסל" }),
        ("@PowerPoint", new CheckBox { Content = "פאוורפוינט" }), ("@Outlook", new CheckBox { Content = "אאוטלוק" }),
        ("@Pdf", new CheckBox { Content = "קורא PDF" }), ("@Notepad", new CheckBox { Content = "פנקס רשימות" }),
        ("@Browsers", new CheckBox { Content = "דפדפנים" })
    };
    readonly TextBox _exceptExtra = new();

    public BlockRule? Result { get; private set; }

    public RuleEditor(Window owner, BlockRule? existing)
    {
        Owner = owner; Title = existing == null ? "חסימה חדשה" : "עריכת חסימה";
        FlowDirection = FlowDirection.RightToLeft; Width = 560; Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _id = existing?.Id ?? Guid.NewGuid();

        var root = new StackPanel { Margin = new Thickness(14) };
        void Add(UIElement e, double top = 6) { if (e is FrameworkElement f) f.Margin = new Thickness(0, top, 0, 0); root.Children.Add(e); }
        TextBlock Label(string t) => new() { Text = t, FontWeight = FontWeights.SemiBold };

        Add(Label("שם החסימה")); Add(_name, 2);
        Add(Label("ימים"), 10);
        var days = new WrapPanel();
        foreach (var (d, n) in DayNames)
        {
            var cb = new CheckBox { Content = n, Margin = new Thickness(0, 0, 12, 0) };
            _days.Add((d, cb)); days.Children.Add(cb);
        }
        Add(days, 2);
        Add(Label("שעות (למשל 10:00 עד 12:00). אם ההתחלה והסוף זהות: כל היום"), 10);
        var times = new StackPanel { Orientation = Orientation.Horizontal };
        times.Children.Add(new TextBlock { Text = "מ-", VerticalAlignment = VerticalAlignment.Center });
        times.Children.Add(_start);
        times.Children.Add(new TextBlock { Text = "  עד  ", VerticalAlignment = VerticalAlignment.Center });
        times.Children.Add(_end);
        Add(times, 2);
        Add(Label("חומרה"), 10);
        _strict.Items.Add("קלה: אפשר לבטל עם הסיסמה");
        _strict.Items.Add("בינונית: אפשר לבטל רק עם קוד החירום");
        _strict.Items.Add("קשה: אי אפשר לבטל עד סוף השעות");
        _strict.SelectedIndex = 0;
        Add(_strict, 2);
        Add(Label("מה לחסום"), 10);
        foreach (var c in new UIElement[] { _all, _wifi, _net, _local, _browsers, _players, _video, _audio }) Add(c, 3);
        Add(new TextBlock { Text = "אתרים לחסימה (אחד בכל שורה, למשל facebook.com)" }, 8); Add(_sites, 2);
        Add(new TextBlock { Text = "תוכנות לחסימה (שם הקובץ, אחד בכל שורה, למשל notepad)" }, 8); Add(_progs, 2);
        Add(_except, 8);
        var apps = new WrapPanel();
        foreach (var (_, b) in _exceptApps) { b.Margin = new Thickness(0, 0, 12, 0); apps.Children.Add(b); }
        Add(apps, 2);
        Add(new TextBlock { Text = "תוכנות נוספות מותרות (שמות קבצים, מופרדים בפסיק)" }, 4); Add(_exceptExtra, 2);

        var ok = new Button { Content = "שמירה", IsDefault = true, Padding = new Thickness(24, 5, 24, 5), Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        ok.Click += (_, _) => Save();
        Add(ok, 14);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        Load(existing);
    }

    static string T(int minute) => $"{minute / 60:00}:{minute % 60:00}";

    static bool ParseTime(string s, out int minute)
    {
        minute = 0;
        var p = s.Trim().Split(':');
        if (p.Length != 2 || !int.TryParse(p[0], out var h) || !int.TryParse(p[1], out var m)) return false;
        if (h is < 0 or > 23 || m is < 0 or > 59) return false;
        minute = h * 60 + m;
        return true;
    }

    static List<string> Split(string s) =>
        s.Split(new[] { '\n', '\r', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    void Load(BlockRule? r)
    {
        if (r == null)
        {
            _name.Text = "שעות עבודה"; _start.Text = "10:00"; _end.Text = "12:00";
            foreach (var (d, b) in _days) b.IsChecked = d != DayOfWeek.Friday && d != DayOfWeek.Saturday;
            return;
        }
        _name.Text = r.Name; _start.Text = T(r.StartMinute); _end.Text = T(r.EndMinute);
        foreach (var (d, b) in _days) b.IsChecked = r.Days.Contains(d);
        _strict.SelectedIndex = (int)r.Strictness;
        _local.IsChecked = r.AllowLocalNetwork;
        foreach (var t in r.Targets)
        {
            switch (t.Kind)
            {
                case TargetKind.AllNetwork: _all.IsChecked = true; break;
                case TargetKind.Wifi: _wifi.IsChecked = true; break;
                case TargetKind.Internet: _net.IsChecked = true; break;
                case TargetKind.Browsers: _browsers.IsChecked = true; break;
                case TargetKind.MediaPlayers: _players.IsChecked = true; break;
                case TargetKind.FileTypes:
                    if (t.Values.Contains("@video")) _video.IsChecked = true;
                    if (t.Values.Contains("@audio")) _audio.IsChecked = true;
                    break;
                case TargetKind.Websites: _sites.Text = string.Join(Environment.NewLine, t.Values); break;
                case TargetKind.Programs: _progs.Text = string.Join(Environment.NewLine, t.Values); break;
                case TargetKind.AllExcept:
                    _except.IsChecked = true;
                    var extra = new List<string>();
                    foreach (var v in t.Values)
                    {
                        var m = _exceptApps.FirstOrDefault(a => a.Token.Equals(v, StringComparison.OrdinalIgnoreCase));
                        if (m.Box != null) m.Box.IsChecked = true; else extra.Add(v);
                    }
                    _exceptExtra.Text = string.Join(", ", extra);
                    break;
            }
        }
    }

    void Save()
    {
        if (!ParseTime(_start.Text, out var s) || !ParseTime(_end.Text, out var e))
        { Dialogs.Error(this, "השעות צריכות להיכתב כך: 10:00"); return; }
        var days = _days.Where(d => d.Box.IsChecked == true).Select(d => d.Day).ToList();
        if (days.Count == 0) { Dialogs.Error(this, "צריך לבחור לפחות יום אחד."); return; }

        var r = new BlockRule
        {
            Id = _id, Name = string.IsNullOrWhiteSpace(_name.Text) ? "חסימה" : _name.Text.Trim(),
            Days = days, StartMinute = s, EndMinute = e, Strictness = (Strictness)_strict.SelectedIndex,
            AllowLocalNetwork = _local.IsChecked == true
        };
        void T1(CheckBox c, TargetKind k) { if (c.IsChecked == true) r.Targets.Add(new Target { Kind = k }); }
        T1(_all, TargetKind.AllNetwork); T1(_wifi, TargetKind.Wifi); T1(_net, TargetKind.Internet);
        T1(_browsers, TargetKind.Browsers); T1(_players, TargetKind.MediaPlayers);
        var types = new List<string>();
        if (_video.IsChecked == true) types.Add("@video");
        if (_audio.IsChecked == true) types.Add("@audio");
        if (types.Count > 0) r.Targets.Add(new Target { Kind = TargetKind.FileTypes, Values = types });

        var sites = Split(_sites.Text);
        var bad = sites.Where(x => Catalog.NormalizeSite(x) == null).ToList();
        if (bad.Count > 0) { Dialogs.Error(this, "כתובת אתר לא תקינה: " + bad[0]); return; }
        if (sites.Count > 0) r.Targets.Add(new Target { Kind = TargetKind.Websites, Values = sites });
        var progs = Split(_progs.Text).Where(x => Catalog.NormalizeProcess(x) != null).ToList();
        if (progs.Count > 0) r.Targets.Add(new Target { Kind = TargetKind.Programs, Values = progs });

        if (_except.IsChecked == true)
        {
            var allowed = _exceptApps.Where(a => a.Box.IsChecked == true).Select(a => a.Token).ToList();
            allowed.AddRange(Split(_exceptExtra.Text));
            if (allowed.Count == 0) { Dialogs.Error(this, "במצב \"הכול חסום חוץ מ...\" צריך לסמן לפחות תוכנה אחת מותרת."); return; }
            r.Targets.Add(new Target { Kind = TargetKind.AllExcept, Values = allowed });
        }
        if (r.Targets.Count == 0) { Dialogs.Error(this, "צריך לבחור לפחות דבר אחד לחסום."); return; }

        if (r.Strictness == Strictness.Hard &&
            !Dialogs.Confirm(this, "חסימה קשה לא ניתנת לביטול עד סוף השעות, גם לא על ידך. להמשיך?")) return;
        Result = r;
        DialogResult = true;
    }
}
