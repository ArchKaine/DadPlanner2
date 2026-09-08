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
        var allLogs = logs.ToList();
        var validLogs = allLogs
            .Where(log => log.Volume is not "None" and not "N/A")
            .OrderBy(log => log.Timestamp)
            .Where(log => !HasThermalShadow(allLogs, log.Timestamp))
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
        return WithConfidenceCounts(new SupplementComparison(
            key,
            days,
            saturated.Count,
            unsaturated.Count,
            saturated.Count(log => log.Volume is "Normal" or "High"),
            unsaturated.Count(log => log.Volume is "Normal" or "High"),
            null,
            null,
            audits), saturated, unsaturated);
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
        var saturatedSessions = gaps.Zip(audits).Where(item => item.Second.IsSaturated).Select(item => item.First.Current).ToList();
        var unsaturatedSessions = gaps.Zip(audits).Where(item => !item.Second.IsSaturated).Select(item => item.First.Current).ToList();
        return WithConfidenceCounts(new SupplementComparison(
            key,
            days,
            saturated.Count,
            unsaturated.Count,
            null,
            null,
            saturated.Count == 0 ? null : saturated.Average(),
            unsaturated.Count == 0 ? null : unsaturated.Average(),
            audits), saturatedSessions, unsaturatedSessions);
    }

    private static SupplementComparison WithConfidenceCounts(
        SupplementComparison comparison,
        IEnumerable<LogRecord> saturated,
        IEnumerable<LogRecord> unsaturated)
    {
        var saturatedSessions = saturated.ToList();
        var unsaturatedSessions = unsaturated.ToList();
        var saturatedCounts = ConfidenceCounts.From(saturatedSessions);
        var unsaturatedCounts = ConfidenceCounts.From(unsaturatedSessions);

        return comparison with
        {
            SaturatedConfidenceCounts = saturatedCounts,
            UnsaturatedConfidenceCounts = unsaturatedCounts,
            SaturatedSuccessesByConfidence = ConfidenceCounts.From(
                saturatedSessions.Where(IsSuccessful)),
            UnsaturatedSuccessesByConfidence = ConfidenceCounts.From(
                unsaturatedSessions.Where(IsSuccessful))
        };
    }

    private static bool IsSuccessful(LogRecord log) => log.Volume is "Normal" or "High";

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

    public ConfidenceCounts SaturatedConfidenceCounts { get; init; } = ConfidenceCounts.Empty;
    public ConfidenceCounts UnsaturatedConfidenceCounts { get; init; } = ConfidenceCounts.Empty;
    public ConfidenceCounts SaturatedSuccessesByConfidence { get; init; } = ConfidenceCounts.Empty;
    public ConfidenceCounts UnsaturatedSuccessesByConfidence { get; init; } = ConfidenceCounts.Empty;

    // Named aliases keep the count intent obvious to callers and exports.
    public ConfidenceCounts SaturatedCountsByConfidence => SaturatedConfidenceCounts;
    public ConfidenceCounts UnsaturatedCountsByConfidence => UnsaturatedConfidenceCounts;
    public ConfidenceCounts SaturatedSuccessCountsByConfidence => SaturatedSuccessesByConfidence;
    public ConfidenceCounts UnsaturatedSuccessCountsByConfidence => UnsaturatedSuccessesByConfidence;

    public int SaturatedObservedCount => SaturatedConfidenceCounts.Observed;
    public int SaturatedEstimatedCount => SaturatedConfidenceCounts.Estimated;
    public int SaturatedUnknownCount => SaturatedConfidenceCounts.Unknown;
    public int UnsaturatedObservedCount => UnsaturatedConfidenceCounts.Observed;
    public int UnsaturatedEstimatedCount => UnsaturatedConfidenceCounts.Estimated;
    public int UnsaturatedUnknownCount => UnsaturatedConfidenceCounts.Unknown;
    public int SaturatedObservedSuccesses => SaturatedSuccessesByConfidence.Observed;
    public int SaturatedEstimatedSuccesses => SaturatedSuccessesByConfidence.Estimated;
    public int SaturatedUnknownSuccesses => SaturatedSuccessesByConfidence.Unknown;
    public int UnsaturatedObservedSuccesses => UnsaturatedSuccessesByConfidence.Observed;
    public int UnsaturatedEstimatedSuccesses => UnsaturatedSuccessesByConfidence.Estimated;
    public int UnsaturatedUnknownSuccesses => UnsaturatedSuccessesByConfidence.Unknown;
    public int SaturatedObservedSuccessCount => SaturatedObservedSuccesses;
    public int SaturatedEstimatedSuccessCount => SaturatedEstimatedSuccesses;
    public int SaturatedUnknownSuccessCount => SaturatedUnknownSuccesses;
    public int UnsaturatedObservedSuccessCount => UnsaturatedObservedSuccesses;
    public int UnsaturatedEstimatedSuccessCount => UnsaturatedEstimatedSuccesses;
    public int UnsaturatedUnknownSuccessCount => UnsaturatedUnknownSuccesses;
}

public sealed record ConfidenceCounts(int Observed, int Estimated, int Unknown)
    : IReadOnlyDictionary<VolumeConfidence, int>
{
    public static ConfidenceCounts Empty { get; } = new(0, 0, 0);
    public int Total => Observed + Estimated + Unknown;

    public int this[VolumeConfidence confidence] => confidence switch
    {
        VolumeConfidence.Observed => Observed,
        VolumeConfidence.Estimated => Estimated,
        _ => Unknown
    };

    public static ConfidenceCounts From(IEnumerable<LogRecord> logs)
    {
        var counts = logs.GroupBy(log => log.VolumeConfidence)
            .ToDictionary(group => group.Key, group => group.Count());
        return new ConfidenceCounts(
            counts.GetValueOrDefault(VolumeConfidence.Observed),
            counts.GetValueOrDefault(VolumeConfidence.Estimated),
            counts.GetValueOrDefault(VolumeConfidence.Unknown));
    }

    public IEnumerable<VolumeConfidence> Keys =>
        Enum.GetValues<VolumeConfidence>();

    public IEnumerable<int> Values => Keys.Select(confidence => this[confidence]);
    public int Count => 3;
    public bool ContainsKey(VolumeConfidence key) => Enum.IsDefined(key);
    public bool TryGetValue(VolumeConfidence key, out int value)
    {
        if (!ContainsKey(key))
        {
            value = 0;
            return false;
        }

        value = this[key];
        return true;
    }

    public IEnumerator<KeyValuePair<VolumeConfidence, int>> GetEnumerator() =>
        Keys.Select(key => new KeyValuePair<VolumeConfidence, int>(key, this[key])).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();
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
