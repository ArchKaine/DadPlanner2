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
        return Analyze(logs, (timestamp, supplement, days) =>
            new SupplementSaturationResult(
                timestamp,
                supplement,
                days,
                0,
                0,
                0,
                isSaturated(timestamp, supplement, days)));
    }

    public SupplementAnalysisResult Analyze(
        IEnumerable<LogRecord> logs,
        Func<long, string, int, SupplementSaturationResult> getSaturation)
    {
        var validLogs = logs
            .Where(log => log.Volume is not "None" and not "N/A")
            .OrderBy(log => log.Timestamp)
            .Where(log => !HasThermalShadow(logs, log.Timestamp))
            .ToList();

        if (validLogs.Count < 5)
            return SupplementAnalysisResult.Insufficient;

        var zinc = BuildYieldComparison(validLogs, "zinc", 21, getSaturation);
        var vitaminD = BuildYieldComparison(validLogs, "vitD", 30, getSaturation);
        var maca = BuildGapComparison(validLogs, "maca", 21, getSaturation);
        var vitaminC = BuildGapComparison(validLogs, "vitC", 30, getSaturation);

        return new SupplementAnalysisResult(zinc, maca, vitaminD, vitaminC, false);
    }

    private static SupplementComparison BuildYieldComparison(
        IReadOnlyList<LogRecord> logs,
        string key,
        int days,
        Func<long, string, int, SupplementSaturationResult> getSaturation)
    {
        var audits = logs.Select(log => getSaturation(log.Timestamp, key, days)).ToList();
        var saturated = logs.Zip(audits).Where(item => item.Second.IsSaturated).Select(item => item.First).ToList();
        var unsaturated = logs.Zip(audits).Where(item => !item.Second.IsSaturated).Select(item => item.First).ToList();
        return new SupplementComparison(
            key,
            days,
            saturated.Count,
            unsaturated.Count,
            saturated.Count(log => log.Volume is "Normal" or "High"),
            unsaturated.Count(log => log.Volume is "Normal" or "High"),
            null,
            null,
            audits);
    }

    private static SupplementComparison BuildGapComparison(
        IReadOnlyList<LogRecord> logs,
        string key,
        int days,
        Func<long, string, int, SupplementSaturationResult> getSaturation)
    {
        var gaps = logs.Zip(logs.Skip(1), (current, previous) =>
            (Current: current, Hours: (current.Timestamp - previous.Timestamp) / 3600.0));
        var audits = gaps.Select(item => getSaturation(item.Current.Timestamp, key, days)).ToList();
        var saturated = gaps.Zip(audits).Where(item => item.Second.IsSaturated).Select(item => item.First.Hours).ToList();
        var unsaturated = gaps.Zip(audits).Where(item => !item.Second.IsSaturated).Select(item => item.First.Hours).ToList();
        return new SupplementComparison(
            key,
            days,
            saturated.Count,
            unsaturated.Count,
            null,
            null,
            saturated.Count == 0 ? null : saturated.Average(),
            unsaturated.Count == 0 ? null : unsaturated.Average(),
            audits);
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
    double? UnsaturatedAverageGap,
    IReadOnlyList<SupplementSaturationResult> SaturationAudits)
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
        new(supplement, days, 0, 0, null, null, null, null, Array.Empty<SupplementSaturationResult>());
}
