using System;
using System.IO;
using System.Linq;
using DadPlanner2.Models;
using DadPlanner2.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DadPlanner2.Tests;

[TestClass]
public sealed class DataExportServiceTests
{
    [TestMethod]
    public void Export_AnalysisIncludesConfidenceCountsInJsonAndCsv()
    {
        string directory = Path.Combine("test-artifacts", $"export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var logs = Enumerable.Range(0, 6)
                .Select(index => new LogRecord
                {
                    Timestamp = 100_000 + (index * 100_000),
                    Volume = index % 2 == 0 ? "High" : "Low",
                    VolumeConfidence = (VolumeConfidence)(index % 3)
                })
                .ToList();
            var analysis = new SupplementAnalysisService().Analyze(
                logs,
                (timestamp, key, days) => new SupplementSaturationResult(
                    timestamp, key, days, 1, 1, 1,
                    key == "zinc" && (timestamp / 100_000) % 2 == 1));

            var result = new DataExportService().Export(logs, directory, analysis);
            string json = File.ReadAllText(result.AnalysisJsonPath!);
            string csv = File.ReadAllText(result.AnalysisCsvPath!);

            StringAssert.Contains(json, "SaturatedConfidenceCounts");
            StringAssert.Contains(json, "SaturatedSuccessesByConfidence");
            StringAssert.Contains(csv, "SaturatedObservedCount");
            StringAssert.Contains(csv, "UnsaturatedUnknownSuccesses");
            StringAssert.Contains(csv, ",1,1,1,");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
