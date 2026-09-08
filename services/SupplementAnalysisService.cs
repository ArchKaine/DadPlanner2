using System;
using System.Collections.Generic;
using System.Linq;
using DadPlanner2.Models;

namespace DadPlanner2.Services;

public sealed class SupplementAnalysisService
{
    public SupplementAnalysisResult Analyze(
        IEnumerable<LogRecord> logs,
        Func<long, string, int, bool> isSaturated)
    {
        var validLogs = logs
            .Where(log => log.Volume is not "None" and not "N/A")
            .OrderBy(log => log.Timestamp)
            .Where(log => !HasThermalShadow(logs, log.Timestamp))
            .ToList();

        if (validLogs.Count < 5)
            return SupplementAnalysisResult.Insufficient;

        var zinc = BuildYieldComparison(validLogs, "zinc", 21, isSaturated);
        var vitaminD = BuildYieldComparison(validLogs, "vitD", 30, isSaturated);
        var maca = BuildGapComparison(validLogs, "maca", 21, isSaturated);
        var vitaminC = BuildGapComparison(validLogs, "vitC", 30, isSaturated);

        return new SupplementAnalysisResult(zinc, maca, vitaminD, vitaminC, false);
    }

    private static SupplementComparison BuildYieldComparison(
        IReadOnlyList<LogRecord> logs,
        string key,
        int days,
        Func<long, string, int, bool> isSaturated)
    {
        var saturated = logs.Where(log => isSaturated(log.Timestamp, key, days)).ToList();
        var unsaturated = logs.Where(log => !isSaturated(log.Timestamp, key, days)).ToList();
        return new SupplementComparison(
            key,
            days,
            saturated.Count,
            unsaturated.Count,
            saturated.Count(log => log.Volume is "Normal" or "High"),
            unsaturated.Count(log => log.Volume is "Normal" or "High"),
            null,
            null);
    }

    private static SupplementComparison BuildGapComparison(
        IReadOnlyList<LogRecord> logs,
        string key,
        int days,
        Func<long, string, int, bool> isSaturated)
    {
        var gaps = logs.Zip(logs.Skip(1), (current, previous) =>
            (Current: current, Hours: (current.Timestamp - previous.Timestamp) / 3600.0));
        var saturated = gaps.Where(item => isSaturated(item.Current.Timestamp, key, days)).Select(item => item.Hours).ToList();
        var unsaturated = gaps.Where(item => !isSaturated(item.Current.Timestamp, key, days)).Select(item => item.Hours).ToList();
        return new SupplementComparison(
            key,
            days,
            saturated.Count,
            unsaturated.Count,
            null,
            null,
            saturated.Count == 0 ? null : saturated.Average(),
            unsaturated.Count == 0 ? null : unsaturated.Average());
    }

    private static bool HasThermalShadow(IEnumerable<LogRecord> logs, long timestamp)
    {
        long windowStart = timestamp - (TelemetryAnalysisService.ThermalShadowDays * 24L * 3600L);
        return logs.Any(log => log.Timestamp >= windowStart && log.Timestamp < timestamp && log.HeatFlag >= 2);
    }
}

public sealed record SupplementComparison(
    string Supplement,
    int WindowDays,
    int SaturatedCount,
    int UnsaturatedCount,
    int? SaturatedSuccesses,
    int? UnsaturatedSuccesses,
    double? SaturatedAverageGap,
    double? UnsaturatedAverageGap)
{
    public bool HasBothGroups => SaturatedCount > 0 && UnsaturatedCount > 0;
}

public sealed record SupplementAnalysisResult(
    SupplementComparison Zinc,
    SupplementComparison Maca,
    SupplementComparison VitaminD,
    SupplementComparison VitaminC,
    bool InsufficientData)
{
    public static SupplementAnalysisResult Insufficient { get; } = new(
        Empty("zinc", 21),
        Empty("maca", 21),
        Empty("vitD", 30),
        Empty("vitC", 30),
        true);

    private static SupplementComparison Empty(string supplement, int days) =>
        new(supplement, days, 0, 0, null, null, null, null);
}
