using System;

namespace DadPlanner2.Models;

public sealed class LogEditHistory
{
    public long Id { get; set; }
    public long LogId { get; set; }
    public long EditedAt { get; set; }
    public string Summary { get; set; } = "";
    public long PreviousTimestamp { get; set; }
    public string PreviousMode { get; set; } = "Maintenance";
    public string PreviousVolume { get; set; } = "Normal";
    public int PreviousReleaseCount { get; set; } = 1;
    public VolumeConfidence PreviousVolumeConfidence { get; set; } = VolumeConfidence.Unknown;
    public int PreviousHeatFlag { get; set; }
    public string PreviousSupplements { get; set; } = "{}";
    public double PreviousClinicalVol { get; set; }
    public int PreviousConcentration { get; set; }
    public int PreviousMotility { get; set; }
    public int PreviousProgMotility { get; set; }
    public int PreviousMorphology { get; set; }
    public double PreviousPhLevel { get; set; }

    public string DisplayText =>
        $"{DateTimeOffset.FromUnixTimeSeconds(EditedAt).ToLocalTime():MMM dd yyyy, HH:mm} - {Summary}";
}
