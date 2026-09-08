using System;
using System.Collections.Generic;
using System.Linq;
using DadPlanner2.Models;

namespace DadPlanner2.Services;

public sealed class SupplementSaturationService
{
    public const double SaturationThreshold = 0.5;

    public SupplementSaturationResult Calculate(
        IEnumerable<LogRecord> logs,
        long targetTimestamp,
        string supplement,
        int windowDays)
    {
        long windowStart = targetTimestamp - windowDays * 24L * 3600L;
        var releaseLogs = logs
            .Where(log =>
                log.Timestamp >= windowStart &&
                log.Timestamp <= targetTimestamp &&
                log.Volume != "None" &&
                log.Volume != "N/A")
            .ToList();
        int supplementEvents = releaseLogs.Count(log =>
            log.Supplements.Contains($"\"{supplement}\":1", StringComparison.Ordinal));
        double proportion = releaseLogs.Count == 0
            ? 0
            : supplementEvents / (double)releaseLogs.Count;

        return new SupplementSaturationResult(
            targetTimestamp,
            supplement,
            windowDays,
            releaseLogs.Count,
            supplementEvents,
            proportion,
            proportion >= SaturationThreshold);
    }
}

public sealed record SupplementSaturationResult(
    long TargetTimestamp,
    string Supplement,
    int WindowDays,
    int ReleaseEventCount,
    int SupplementEventCount,
    double SupplementProportion,
    bool IsSaturated);
