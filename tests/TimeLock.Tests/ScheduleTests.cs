using Xunit;
namespace TimeLock.Tests;

public class ScheduleTests
{
    static BlockRule Rule(int s, int e, params DayOfWeek[] days) =>
        new() { StartMinute = s, EndMinute = e, Days = days.ToList() };

    // 2026-11-23 הוא יום שני
    static DateTime At(int day, int h, int m) => new(2026, 11, day, h, m, 0);

    [Fact]
    public void InsideAndOutsideSameDay()
    {
        var r = Rule(10 * 60, 12 * 60, DayOfWeek.Monday);
        Assert.False(Schedule.IsActive(r, At(23, 9, 59)));
        Assert.True(Schedule.IsActive(r, At(23, 10, 0)));
        Assert.True(Schedule.IsActive(r, At(23, 11, 59)));
        Assert.False(Schedule.IsActive(r, At(23, 12, 0)));
    }

    [Fact]
    public void WrongDayIsInactive()
    {
        var r = Rule(10 * 60, 12 * 60, DayOfWeek.Tuesday);
        Assert.False(Schedule.IsActive(r, At(23, 11, 0)));
        Assert.True(Schedule.IsActive(r, At(24, 11, 0)));
    }

    [Fact]
    public void CrossMidnightBelongsToStartDay()
    {
        var r = Rule(22 * 60, 6 * 60, DayOfWeek.Monday);
        Assert.True(Schedule.IsActive(r, At(23, 23, 0)));   // שני בלילה
        Assert.True(Schedule.IsActive(r, At(24, 5, 59)));   // שלישי לפנות בוקר
        Assert.False(Schedule.IsActive(r, At(24, 6, 0)));
        Assert.False(Schedule.IsActive(r, At(23, 5, 0)));   // שני בבוקר: לא
        Assert.False(Schedule.IsActive(r, At(24, 23, 0)));  // שלישי בלילה: לא
    }

    [Fact]
    public void SundayBeforeMondayWrapsCorrectly()
    {
        var r = Rule(22 * 60, 6 * 60, DayOfWeek.Sunday);
        Assert.True(Schedule.IsActive(r, At(23, 3, 0))); // שני 03:00 שייך לחסימה של ראשון
    }

    [Fact]
    public void AllDayAndDisabled()
    {
        var r = Rule(0, 0, DayOfWeek.Monday);
        Assert.True(Schedule.IsActive(r, At(23, 15, 0)));
        r.Enabled = false;
        Assert.False(Schedule.IsActive(r, At(23, 15, 0)));
    }

    [Fact]
    public void RemainingIsCorrect()
    {
        var r = Rule(10 * 60, 12 * 60, DayOfWeek.Monday);
        Assert.Equal(TimeSpan.FromMinutes(30), Schedule.Remaining(r, At(23, 11, 30)));
    }
}
