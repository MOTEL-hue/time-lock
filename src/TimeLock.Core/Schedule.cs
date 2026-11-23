namespace TimeLock;

public static class Schedule
{
    public static bool IsActive(BlockRule r, DateTime now)
    {
        if (!r.Enabled || r.Days.Count == 0) return false;
        int m = now.Hour * 60 + now.Minute;
        if (r.StartMinute == r.EndMinute) return r.Days.Contains(now.DayOfWeek);
        if (r.StartMinute < r.EndMinute)
            return r.Days.Contains(now.DayOfWeek) && m >= r.StartMinute && m < r.EndMinute;
        // חוצה חצות: אחרי ההתחלה שייך ליום ההתחלה, לפני הסוף שייך ליום שלפניו
        if (m >= r.StartMinute) return r.Days.Contains(now.DayOfWeek);
        if (m < r.EndMinute) return r.Days.Contains((DayOfWeek)(((int)now.DayOfWeek + 6) % 7));
        return false;
    }

    /// <summary>הזמן שנשאר עד סוף החסימה הפעילה.</summary>
    public static TimeSpan Remaining(BlockRule r, DateTime now)
    {
        if (r.StartMinute == r.EndMinute) return now.Date.AddDays(1) - now;
        var end = now.Date.AddMinutes(r.EndMinute);
        if (end <= now) end = end.AddDays(1);
        return end - now;
    }
}
