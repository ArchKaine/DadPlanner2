using DadPlanner2.Models;
using DadPlanner2.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DadPlanner2.Tests;

[TestClass]
public sealed class TelemetryAnalysisServiceTests
{
    private readonly TelemetryAnalysisService _service = new();

    [TestMethod]
    public void CalculateRecoveryMetrics_UsesDescendingReleaseEvents()
    {
        const long now = 10_000;
        var logs = new[] { Log(1_000, "None"), Log(4_600, "Normal"), Log(8_200, "High"), Log(9_000, "None") };

        var result = _service.CalculateRecoveryMetrics(logs, now);

        Assert.AreEqual(0.5, result.CurrentHours);
        Assert.AreEqual(1, result.AverageGapHours);
        Assert.AreEqual(1, result.MaximumGapHours);
        Assert.IsTrue(result.HasRelease);
    }

    [TestMethod]
    public void CalculateRecoveryMetrics_WithNoReleaseEvents_ReturnsEmpty()
    {
        var result = _service.CalculateRecoveryMetrics(new[] { Log(1_000, "None") }, 10_000);

        Assert.IsFalse(result.HasRelease);
        Assert.AreEqual(0, result.CurrentHours);
        Assert.IsNull(result.AverageGapHours);
        Assert.IsNull(result.MaximumGapHours);
    }

    [TestMethod]
    public void HasActiveThermalShadow_IncludesHeatAtExactBoundary()
    {
        const long now = 100_000;
        var heat = Log(now - (TelemetryAnalysisService.ThermalShadowDays * 24L * 3600L), "Normal", heatFlag: 2);

        var active = _service.HasActiveThermalShadow(new[] { heat }, now, out var latestHeat);

        Assert.IsTrue(active);
        Assert.AreSame(heat, latestHeat);
    }

    [TestMethod]
    public void HasActiveThermalShadow_ClearsAfterPassingClinicalLab()
    {
        const long now = 100_000;
        var heat = Log(now - 10_000, "Normal", heatFlag: 2);
        var lab = Log(now - 5_000, "High", mode: "Clinical-Lab", concentration: 15, motility: 40);

        var active = _service.HasActiveThermalShadow(new[] { heat, lab }, now, out _);

        Assert.IsFalse(active);
    }

    [TestMethod]
    public void EstimateVolume_UsesThresholdsAndZincSaturation()
    {
        var logs = new[] { Log(1_000, "Normal") };

        var low = _service.EstimateVolume(logs, 1_000 + (12 * 3600), 24, 72, (_, _, _) => false);
        var saturated = _service.EstimateVolume(logs, 1_000 + (12 * 3600), 24, 72, (_, _, _) => true);
        var high = _service.EstimateVolume(logs, 1_000 + (72 * 3600), 24, 72, (_, _, _) => false);

        Assert.AreEqual("Low", low);
        Assert.AreEqual("Normal", saturated);
        Assert.AreEqual("High", high);
    }

    [TestMethod]
    public void MultiReleaseSession_IsOneRecoveryEventButCountsReleasesForFrequency()
    {
        const long now = 10_000;
        var session = Log(4_600, "Normal");
        session.ReleaseCount = 3;
        var result = _service.CalculateRecoveryMetrics(new[] { session }, now);

        Assert.AreEqual(1.5, result.CurrentHours);
        Assert.IsNull(result.AverageGapHours);
        Assert.AreEqual(21, _service.CalculateFrequencyPerWeek(new[] { session }, now));
    }

    private static LogRecord Log(
        long timestamp,
        string volume,
        int heatFlag = 0,
        string mode = "Maintenance",
        int concentration = 0,
        int motility = 0)
    {
        return new LogRecord
        {
            Timestamp = timestamp,
            Volume = volume,
            HeatFlag = heatFlag,
            Mode = mode,
            Concentration = concentration,
            Motility = motility
        };
    }
}
