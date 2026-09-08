using System;
using System.Collections.Generic;
using System.Linq;
using DadPlanner2.Models;
using LiveChartsCore.Defaults;

namespace DadPlanner2.Services;

public sealed class ReportDataService
{
    public const int ReportWindowDays = 90;

    public ReportData Create(IEnumerable<LogRecord> sourceLogs, long nowTimestamp)
    {
        long cutoff = nowTimestamp - ReportWindowDays * 24L * 3600;
        var logs = sourceLogs
            .Where(log => log.Timestamp >= cutoff)
            .OrderBy(log => log.Timestamp)
            .ToList();
        var releaseLogs = logs
            .Where(log => log.Volume != "None" && log.Volume != "N/A")
            .ToList();

        double averageGap = 0;
        double minimumGap = 0;
        var gapData = new List<DateTimePoint>();
        if (releaseLogs.Count > 0)
        {
            double totalGap = 0;
            minimumGap = 999;
            for (int i = 0; i < releaseLogs.Count; i++)
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(releaseLogs[i].Timestamp).ToLocalTime().DateTime;
                double gap = i == 0
                    ? 72.0
                    : (releaseLogs[i].Timestamp - releaseLogs[i - 1].Timestamp) / 3600.0;
                gapData.Add(new DateTimePoint(date, gap));
                if (i > 0)
                {
                    totalGap += gap;
                    if (gap < minimumGap) minimumGap = gap;
                }
            }

            if (releaseLogs.Count > 1)
                averageGap = totalGap / (releaseLogs.Count - 1);
        }

        return new ReportData(
            logs,
            gapData,
            averageGap,
            minimumGap,
            logs.Count(log => log.Mode == "Maintenance"),
            logs.Count(log => log.Mode == "Playtime"),
            logs.Count(log => log.Mode == "Baby-Making"),
            logs.Count(log => log.Mode == "Clinical-Lab"),
            logs.Count(log => log.Volume == "High"),
            logs.Count(log => log.Volume == "Normal"),
            logs.Count(log => log.Volume == "Low"),
            logs.Count(log => log.Volume is "None" or "N/A"));
    }
}

public sealed record ReportData(
    IReadOnlyList<LogRecord> Logs,
    List<DateTimePoint> GapData,
    double AverageGap,
    double MinimumGap,
    int MaintenanceCount,
    int PlaytimeCount,
    int BabyMakingCount,
    int ClinicalLabCount,
    int HighCount,
    int NormalCount,
    int LowCount,
    int DryCount);
