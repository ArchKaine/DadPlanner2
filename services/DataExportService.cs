using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DadPlanner2.Models;

namespace DadPlanner2.Services;

public sealed class DataExportService
{
    public DataExportResult Export(
        IEnumerable<LogRecord> sourceLogs,
        string exportDirectory,
        SupplementAnalysisResult? analysis = null,
        IEnumerable<LogEditHistory>? sourceHistory = null)
    {
        Directory.CreateDirectory(exportDirectory);
        var logs = sourceLogs.OrderBy(log => log.Timestamp).ToList();
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        string jsonPath = Path.Combine(exportDirectory, $"dadplanner-export-{timestamp}.json");
        string csvPath = Path.Combine(exportDirectory, $"dadplanner-export-{timestamp}.csv");
        var history = sourceHistory?.OrderBy(entry => entry.EditedAt).ToList() ?? new List<LogEditHistory>();
        string historyJsonPath = Path.Combine(exportDirectory, $"dadplanner-edit-history-{timestamp}.json");
        string historyCsvPath = Path.Combine(exportDirectory, $"dadplanner-edit-history-{timestamp}.csv");

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(logs, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        }));

        using (var writer = new StreamWriter(csvPath))
        {
            writer.WriteLine("Id,Timestamp,Date,Mode,Volume,ReleaseCount,VolumeConfidence,HeatFlag,Supplements,ClinicalVol,Concentration,Motility,ProgMotility,Morphology,PhLevel,HasPdf,AbstinenceHours,WhoCompliance,ThermalStage");

            for (int i = 0; i < logs.Count; i++)
            {
                var log = logs[i];

                // Calculate abstinence gap from prior release
                string abstinenceStr = "";
                string whoComplianceStr = "";
                var priorRelease = logs.Take(i).Where(l => l.Volume != "None" && l.Volume != "N/A").LastOrDefault();
                if (priorRelease != null && log.Volume != "None" && log.Volume != "N/A")
                {
                    double gapHours = (log.Timestamp - priorRelease.Timestamp) / 3600.0;
                    abstinenceStr = gapHours.ToString("F1", CultureInfo.InvariantCulture);

                    if (log.Mode == "Clinical-Lab")
                    {
                        if (gapHours >= 48.0 && gapHours <= 72.0) whoComplianceStr = "WHO Ideal (48-72h)";
                        else if (gapHours >= 48.0 && gapHours <= 168.0) whoComplianceStr = "WHO Acceptable (48-168h)";
                        else if (gapHours < 48.0) whoComplianceStr = "Sub-48h (<48h)";
                        else whoComplianceStr = "Extended (>7d)";
                    }
                }

                // Calculate thermal shadow stage at this session
                string thermalStageStr = GetThermalStageAt(logs.Take(i).ToList(), log.Timestamp);

                writer.WriteLine(string.Join(",",
                    log.Id,
                    log.Timestamp,
                    CsvEscape(log.DisplayDate),
                    CsvEscape(log.Mode),
                    CsvEscape(log.Volume),
                    log.ReleaseCount,
                    CsvEscape(log.VolumeConfidence.ToString()),
                    log.HeatFlag,
                    CsvEscape(log.Supplements),
                    log.ClinicalVol.ToString(CultureInfo.InvariantCulture),
                    log.Concentration,
                    log.Motility,
                    log.ProgMotility,
                    log.Morphology,
                    log.PhLevel.ToString(CultureInfo.InvariantCulture),
                    log.HasPdf,
                    abstinenceStr,
                    CsvEscape(whoComplianceStr),
                    CsvEscape(thermalStageStr)));
            }
        }

        File.WriteAllText(historyJsonPath, JsonSerializer.Serialize(history, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        using (var historyWriter = new StreamWriter(historyCsvPath))
        {
            historyWriter.WriteLine("Id,LogId,EditedAt,Date,Summary");
            foreach (var entry in history)
            {
                int sepIdx = entry.DisplayText.IndexOf(" - ", StringComparison.Ordinal);
                string datePart = sepIdx > 0 ? entry.DisplayText[..sepIdx] : entry.DisplayText;

                historyWriter.WriteLine(string.Join(",",
                    entry.Id,
                    entry.LogId,
                    entry.EditedAt,
                    CsvEscape(datePart),
                    CsvEscape(entry.Summary)));
            }
        }

        string? analysisJsonPath = null;
        string? analysisCsvPath = null;
        if (analysis != null)
        {
            analysisJsonPath = Path.Combine(exportDirectory, $"dadplanner-analysis-{timestamp}.json");
            analysisCsvPath = Path.Combine(exportDirectory, $"dadplanner-analysis-{timestamp}.csv");

            File.WriteAllText(analysisJsonPath, JsonSerializer.Serialize(analysis, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            }));

            using var analysisWriter = new StreamWriter(analysisCsvPath);
            analysisWriter.WriteLine("Supplement,WindowDays,TargetTimestamp,ReleaseEventCount,SupplementEventCount,SupplementProportion,Classification,SaturatedCount,UnsaturatedCount,SaturatedObservedCount,SaturatedEstimatedCount,SaturatedUnknownCount,UnsaturatedObservedCount,UnsaturatedEstimatedCount,UnsaturatedUnknownCount,SaturatedSuccesses,UnsaturatedSuccesses,SaturatedObservedSuccesses,SaturatedEstimatedSuccesses,SaturatedUnknownSuccesses,UnsaturatedObservedSuccesses,UnsaturatedEstimatedSuccesses,UnsaturatedUnknownSuccesses,KineticSaturation,ConsecutiveWeeksSaturated,IsCyclingRecommended,CyclingMessage,ClinicalNote");

            foreach (var comparison in new[] { analysis.Zinc, analysis.Maca, analysis.VitaminD, analysis.VitaminC })
            {
                foreach (var audit in comparison.SaturationAudits)
                {
                    analysisWriter.WriteLine(string.Join(",",
                        CsvEscape(comparison.Supplement),
                        audit.WindowDays,
                        audit.TargetTimestamp,
                        audit.ReleaseEventCount,
                        audit.SupplementEventCount,
                        audit.SupplementProportion.ToString("F4", CultureInfo.InvariantCulture),
                        audit.IsSaturated ? "SATURATED" : "UNSATURATED",
                        comparison.SaturatedCount,
                        comparison.UnsaturatedCount,
                        comparison.SaturatedObservedCount,
                        comparison.SaturatedEstimatedCount,
                        comparison.SaturatedUnknownCount,
                        comparison.UnsaturatedObservedCount,
                        comparison.UnsaturatedEstimatedCount,
                        comparison.UnsaturatedUnknownCount,
                        comparison.SaturatedSuccesses?.ToString() ?? "",
                        comparison.UnsaturatedSuccesses?.ToString() ?? "",
                        comparison.SaturatedObservedSuccesses,
                        comparison.SaturatedEstimatedSuccesses,
                        comparison.SaturatedUnknownSuccesses,
                        comparison.UnsaturatedObservedSuccesses,
                        comparison.UnsaturatedEstimatedSuccesses,
                        comparison.UnsaturatedUnknownSuccesses,
                        audit.KineticSaturation.ToString("F2", CultureInfo.InvariantCulture),
                        audit.ConsecutiveWeeksSaturated,
                        audit.IsCyclingRecommended,
                        CsvEscape(audit.CyclingMessage),
                        CsvEscape(audit.ClinicalNote)));
                }
            }
        }

        return new DataExportResult(jsonPath, csvPath, analysisJsonPath, analysisCsvPath, historyJsonPath, historyCsvPath);
    }

    private static string GetThermalStageAt(List<LogRecord> logsBeforeTarget, long targetTs)
    {
        var heatLogs = logsBeforeTarget
            .Where(l => l.HeatFlag >= 2)
            .OrderByDescending(l => l.Timestamp)
            .ToList();

        if (heatLogs.Count == 0) return "None";

        var latestHeat = heatLogs[0];
        long elapsedSeconds = targetTs - latestHeat.Timestamp;
        if (elapsedSeconds < 0 || elapsedSeconds > (74L * 86400)) return "None";

        bool passingLab = logsBeforeTarget.Any(l =>
            l.Mode == "Clinical-Lab" &&
            l.Timestamp > latestHeat.Timestamp &&
            l.Concentration >= 15 &&
            l.Motility >= 40);

        if (passingLab) return "Cleared (Lab Override)";

        int daysElapsed = (int)(elapsedSeconds / 86400);
        if (daysElapsed <= 14) return "Stage 1: Epididymal Transit";
        if (daysElapsed <= 45) return "Stage 2: Spermiogenesis Transition";
        return "Stage 3: Meiotic Regeneration";
    }

    private static string CsvEscape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

public sealed record DataExportResult(
    string JsonPath,
    string CsvPath,
    string? AnalysisJsonPath,
    string? AnalysisCsvPath,
    string HistoryJsonPath,
    string HistoryCsvPath);
