using System;
using System.Collections.Generic;
using System.Linq;
using DadPlanner2.Models;

namespace DadPlanner2.Services;

public sealed class TelemetryAnalysisService
{
    public const int ThermalShadowDays = 74;

    public IReadOnlyList<LogRecord> GetReleaseLogs(IEnumerable<LogRecord> logs)
    {
        return logs
            .Where(log => log.Volume is not "None" and not "N/A")
            .OrderByDescending(log => log.Timestamp)
            .ToList();
    }

    public RecoveryMetrics CalculateRecoveryMetrics(IEnumerable<LogRecord> logs, long now)
    {
        var releaseLogs = GetReleaseLogs(logs);
        if (releaseLogs.Count == 0)
            return RecoveryMetrics.Empty;

        double currentHours = (now - releaseLogs[0].Timestamp) / 3600.0;
        if (releaseLogs.Count == 1)
            return new RecoveryMetrics(currentHours, null, null, true);

        var gaps = releaseLogs
            .Zip(releaseLogs.Skip(1), (current, previous) => (current.Timestamp - previous.Timestamp) / 3600.0)
            .ToList();

        return new RecoveryMetrics(currentHours, gaps.Average(), gaps.Max(), true);
    }

    public bool HasActiveThermalShadow(IEnumerable<LogRecord> logs, long now, out LogRecord? latestHeat)
    {
        long shadowStart = now - (ThermalShadowDays * 24L * 3600L);
        var recentHeat = logs
            .Where(log => log.Timestamp >= shadowStart && log.HeatFlag >= 2)
            .OrderByDescending(log => log.Timestamp)
            .FirstOrDefault();
        latestHeat = recentHeat;

        if (recentHeat == null)
            return false;

        return !logs.Any(log =>
            log.Mode == "Clinical-Lab" &&
            log.Timestamp > recentHeat.Timestamp &&
            log.Timestamp <= now &&
            log.Concentration >= 15 &&
            log.Motility >= 40);
    }

    public string EstimateVolume(
        IEnumerable<LogRecord> logs,
        long timestamp,
        double minHours,
        double maxHours,
        Func<long, string, int, bool> isSupplementSaturated)
    {
        var priorRelease = GetReleaseLogs(logs)
            .Where(log => log.Timestamp < timestamp)
            .FirstOrDefault();

        double gapHours = priorRelease == null
            ? maxHours
            : (timestamp - priorRelease.Timestamp) / 3600.0;

        string volume = gapHours < minHours ? "Low" : gapHours >= maxHours ? "High" : "Normal";
        if (isSupplementSaturated(timestamp, "zinc", 21))
            volume = volume == "Low" ? "Normal" : volume == "Normal" ? "High" : volume;

        return volume;
    }
}

public sealed record RecoveryMetrics(double CurrentHours, double? AverageGapHours, double? MaximumGapHours, bool HasRelease)
{
    public static RecoveryMetrics Empty { get; } = new(0, null, null, false);
}
