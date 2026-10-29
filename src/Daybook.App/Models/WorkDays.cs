using System.Globalization;

namespace Daybook.App.Models;

/// <summary>Calendar helpers for "off until" dates. Saturdays and Sundays are not working days.</summary>
public static class WorkDays
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public static DateOnly Tomorrow => NextWorkingDay(Today);

    /// <summary>Friday of this week (next week's Friday on a weekend).</summary>
    public static DateOnly EndOfWeek => Today.AddDays(((int)DayOfWeek.Friday - (int)Today.DayOfWeek + 7) % 7);

    public static DateOnly EndOfNextWeek => EndOfWeek.AddDays(7);

    public static DateOnly NextWorkingDay(DateOnly day)
    {
        do day = day.AddDays(1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        return day;
    }

    /// <summary>"Fri 31": short enough for a tag, English to match the UI.</summary>
    public static string Short(DateOnly day) => day.ToString("ddd d", CultureInfo.InvariantCulture);
}
