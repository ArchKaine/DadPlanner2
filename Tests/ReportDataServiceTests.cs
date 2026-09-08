using System;
using DadPlanner2.Models;
using DadPlanner2.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DadPlanner2.Tests;

[TestClass]
public sealed class ReportDataServiceTests
{
    [TestMethod]
    public void Create_FiltersToNinetyDaysAndBuildsSummary()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var logs = new[]
        {
            new LogRecord { Timestamp = now - 91 * 24L * 3600, Mode = "Playtime", Volume = "High" },
            new LogRecord { Timestamp = now - 72 * 3600, Mode = "Maintenance", Volume = "Normal" },
            new LogRecord { Timestamp = now - 24 * 3600, Mode = "Clinical-Lab", Volume = "Normal" },
            new LogRecord { Timestamp = now - 12 * 3600, Mode = "Playtime", Volume = "None" }
        };

        var report = new ReportDataService().Create(logs, now);

        Assert.AreEqual(3, report.Logs.Count);
        Assert.AreEqual(2, report.GapData.Count);
        Assert.AreEqual(48, report.AverageGap);
        Assert.AreEqual(48, report.MinimumGap);
        Assert.AreEqual(1, report.MaintenanceCount);
        Assert.AreEqual(1, report.ClinicalLabCount);
        Assert.AreEqual(2, report.NormalCount);
        Assert.AreEqual(1, report.DryCount);
    }

    [TestMethod]
    public void Create_EmptyLogsReturnsSafeDefaults()
    {
        var report = new ReportDataService().Create(Array.Empty<LogRecord>(), 1_700_000_000);

        Assert.IsEmpty(report.Logs);
        Assert.IsEmpty(report.GapData);
        Assert.AreEqual(0, report.AverageGap);
        Assert.AreEqual(0, report.MinimumGap);
    }
}
