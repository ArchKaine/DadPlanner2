using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using DadPlanner2.Models;

namespace DadPlanner2.Services;

public sealed class DataExportService
{
    public (string JsonPath, string CsvPath) Export(IEnumerable<LogRecord> sourceLogs, string exportDirectory)
    {
        Directory.CreateDirectory(exportDirectory);
        var logs = sourceLogs.OrderBy(log => log.Timestamp).ToList();
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        string jsonPath = Path.Combine(exportDirectory, $"dadplanner-export-{timestamp}.json");
        string csvPath = Path.Combine(exportDirectory, $"dadplanner-export-{timestamp}.csv");

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true }));

        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("Id,Timestamp,Date,Mode,Volume,HeatFlag,Supplements,ClinicalVol,Concentration,Motility,ProgMotility,Morphology,PhLevel,HasPdf");
        foreach (var log in logs)
        {
            writer.WriteLine(string.Join(",",
                log.Id,
                log.Timestamp,
                CsvEscape(log.DisplayDate),
                CsvEscape(log.Mode),
                CsvEscape(log.Volume),
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

        return (jsonPath, csvPath);
    }

    private static string CsvEscape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
