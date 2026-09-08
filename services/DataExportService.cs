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
        SupplementAnalysisResult? analysis = null)
    {
        Directory.CreateDirectory(exportDirectory);
        var logs = sourceLogs.OrderBy(log => log.Timestamp).ToList();
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        string jsonPath = Path.Combine(exportDirectory, $"dadplanner-export-{timestamp}.json");
        string csvPath = Path.Combine(exportDirectory, $"dadplanner-export-{timestamp}.csv");

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(logs, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        }));

        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("Id,Timestamp,Date,Mode,Volume,ReleaseCount,VolumeConfidence,HeatFlag,Supplements,ClinicalVol,Concentration,Motility,ProgMotility,Morphology,PhLevel,HasPdf");
        foreach (var log in logs)
        {
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
                log.HasPdf));
        }

        string? analysisJsonPath = null;
        string? analysisCsvPath = null;
        if (analysis != null)
        {
            analysisJsonPath = Path.Combine(exportDirectory, $"dadplanner-analysis-{timestamp}.json");
            analysisCsvPath = Path.Combine(exportDirectory, $"dadplanner-analysis-{timestamp}.csv");
            File.WriteAllText(analysisJsonPath, JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true }));

            using var analysisWriter = new StreamWriter(analysisCsvPath);
            analysisWriter.WriteLine("Supplement,WindowDays,TargetTimestamp,ReleaseEventCount,SupplementEventCount,SupplementProportion,Classification");
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
                        audit.IsSaturated ? "SATURATED" : "UNSATURATED"));
                }
            }
        }

        return new DataExportResult(jsonPath, csvPath, analysisJsonPath, analysisCsvPath);
    }

    private static string CsvEscape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

public sealed record DataExportResult(
    string JsonPath,
    string CsvPath,
    string? AnalysisJsonPath,
    string? AnalysisCsvPath);
