using System;
using System.Linq;
using DadPlanner2.Models;
using DadPlanner2.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DadPlanner2.Tests;

[TestClass]
public sealed class SupplementAnalysisServiceTests
{
    [TestMethod]
    public void Analyze_WithTooFewEvents_ReturnsInsufficientData()
    {
        var logs = Enumerable.Range(0, 4).Select(index => Log(index + 1, "Normal"));

        var result = new SupplementAnalysisService().Analyze(logs, (_, _, _) => true);

        Assert.IsTrue(result.InsufficientData);
    }

    [TestMethod]
    public void Analyze_ExcludesEventsInsideThermalShadow()
    {
        var logs = Enumerable.Range(0, 6)
            .Select(index => Log(100_000 + (index * 3600), "Normal"))
            .ToList();
        logs[0].HeatFlag = 2;

        var result = new SupplementAnalysisService().Analyze(logs, (_, _, _) => false);

        Assert.IsTrue(result.InsufficientData);
    }

    [TestMethod]
    public void Analyze_ReportsSuccessCountsForBothGroups()
    {
        var logs = Enumerable.Range(0, 6)
            .Select(index => Log(100_000 + (index * 100_000), index % 2 == 1 ? "High" : "Low"))
            .ToList();

        var result = new SupplementAnalysisService().Analyze(
            logs,
            (timestamp, key, _) => key == "zinc" && (timestamp / 100_000) % 2 == 0);

        Assert.IsFalse(result.InsufficientData);
        Assert.AreEqual(3, result.Zinc.SaturatedCount);
        Assert.AreEqual(3, result.Zinc.UnsaturatedCount);
        Assert.AreEqual(3, result.Zinc.SaturatedSuccesses);
        Assert.AreEqual(0, result.Zinc.UnsaturatedSuccesses);
    }

    private static LogRecord Log(long timestamp, string volume) =>
        new() { Timestamp = timestamp, Volume = volume };
}
