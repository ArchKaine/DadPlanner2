using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DadPlanner2.Converters;

/// <summary>
/// Converts Unix timestamp to relative time string: "2 hours ago", "in 3 days", etc.
/// Bind this converter with a periodic UI refresh to keep displays current.
/// </summary>
public class RelativeTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long timestamp || timestamp == 0)
            return "—";

        var eventTime = UnixTimeStampToDateTime(timestamp);
        var now = DateTime.Now;
        var diff = now - eventTime;

        if (diff.TotalSeconds < 60)
            return "just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 30)
            return $"{(int)diff.TotalDays}d ago";
        
        return eventTime.ToString("MMM d");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;

    private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
        return dateTime;
    }
}

/// <summary>
/// Converts Unix timestamp to countdown: "in 2h 30m", "overdue", etc.
/// Use for appointment/deadline countdowns.
/// </summary>
public class CountdownConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long timestamp || timestamp == 0)
            return "—";

        var deadline = UnixTimeStampToDateTime(timestamp);
        var now = DateTime.Now;
        var diff = deadline - now;

        if (diff.TotalSeconds < 0)
            return "overdue";
        if (diff.TotalHours < 1)
            return $"in {(int)diff.TotalMinutes}m";
        if (diff.TotalDays < 1)
            return $"in {(int)diff.TotalHours}h {(int)(diff.TotalMinutes % 60)}m";
        
        return $"in {(int)diff.TotalDays}d";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;

    private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
        return dateTime;
    }
}
