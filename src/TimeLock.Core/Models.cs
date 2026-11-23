namespace TimeLock;

/// <summary>חומרת חסימה: סיסמה, קוד חירום, או בלי יציאה עד סוף השעות.</summary>
public enum Strictness { Soft, Code, Hard }

public enum TargetKind
{
    AllNetwork,   // כיבוי כל כרטיסי הרשת (כולל רשת מקומית)
    Wifi,         // כיבוי כרטיסי ה-WiFi בלבד
    Internet,     // חסימת אינטרנט בחומת האש (אפשר להשאיר רשת מקומית)
    Websites,     // חסימת אתרים לפי כתובת
    Programs,     // חסימת תוכנות לפי שם קובץ
    Browsers,     // כל סוגי הדפדפנים
    MediaPlayers, // נגני מדיה
    FileTypes,    // סוגי קבצים (למשל וידאו)
    AllExcept     // הכול חסום חוץ מרשימה מותרת
}

public sealed class Target
{
    public TargetKind Kind { get; set; }
    public List<string> Values { get; set; } = new();
}

public sealed class BlockRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<DayOfWeek> Days { get; set; } = new();
    /// <summary>דקות מחצות. אם התחלה == סוף: כל היום.</summary>
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public Strictness Strictness { get; set; } = Strictness.Soft;
    /// <summary>רלוונטי לחסימת אינטרנט: להשאיר רשת מקומית ומדפסת פתוחות.</summary>
    public bool AllowLocalNetwork { get; set; } = true;
    public List<Target> Targets { get; set; } = new();
}

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public string? PasswordHash { get; set; }
    public string? EmergencyCodeHash { get; set; }
    public DateTime? FirstRunUtc { get; set; }
    public string? LicenseKey { get; set; }
    public List<BlockRule> Rules { get; set; } = new();
}
