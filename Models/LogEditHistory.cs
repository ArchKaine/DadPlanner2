using System;

namespace DadPlanner2.Models;

public sealed class LogEditHistory
{
    public long Id { get; set; }
    public long LogId { get; set; }
    public long EditedAt { get; set; }
    public string Summary { get; set; } = "";

    public string DisplayText =>
        $"{DateTimeOffset.FromUnixTimeSeconds(EditedAt).ToLocalTime():MMM dd yyyy, HH:mm} - {Summary}";
}
