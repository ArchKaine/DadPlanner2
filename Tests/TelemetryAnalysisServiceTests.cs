using System;
using DadPlanner2.Models;
using DadPlanner2.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DadPlanner2.Tests;

[TestClass]
public sealed class TelemetryAnalysisServiceTests
{
    private readonly TelemetryAnalysisService _service = new();

    // ==========================================
    // ORIGINAL TESTS (VERIFIED & PRESERVED)
    // ==========================================

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

    // ==========================================
    // 4-PHASE BIOLOGICAL STATE MACHINE TESTS
    // ==========================================

    [TestMethod]
    public void CalculateRecoveryMetrics_IdentifiesRechargingPhase()
    {
        const long now = 100_000;
        // 12 hours ago (Floor is 24h)
        var logs = new[] { Log(now - (12 * 3600), "Normal") };

        var metrics = _service.CalculateRecoveryMetrics(logs, now, minHours: 24, maxHours: 72);

        Assert.AreEqual(TelemetryPhase.Recharging, metrics.Phase);
        Assert.AreEqual("#007acc", metrics.PhaseColorHex);
        Assert.AreEqual(0.5, metrics.ProgressToPeak, 0.01);
        Assert.IsTrue(metrics.ViabilityPercentage < 1.0);
    }

    [TestMethod]
    public void CalculateRecoveryMetrics_IdentifiesPeakWindowPhase()
    {
        const long now = 100_000;
        // 48 hours ago (Floor is 24h, Ceiling is 72h)
        var logs = new[] { Log(now - (48 * 3600), "Normal") };

        var metrics = _service.CalculateRecoveryMetrics(logs, now, minHours: 24, maxHours: 72);

        Assert.AreEqual(TelemetryPhase.PeakWindow, metrics.Phase);
        Assert.AreEqual("#4caf50", metrics.PhaseColorHex);
        Assert.AreEqual(1.0, metrics.ViabilityPercentage);
        Assert.AreEqual(1.0, metrics.ProgressToPeak);
    }

    [TestMethod]
    public void CalculateRecoveryMetrics_IdentifiesExtendedStoragePhase()
    {
        const long now = 100_000;
        // 96 hours ago (Ceiling is 72h, Fade begins at 120h)
        var logs = new[] { Log(now - (96 * 3600), "Normal") };

        var metrics = _service.CalculateRecoveryMetrics(logs, now, minHours: 24, maxHours: 72);

        Assert.AreEqual(TelemetryPhase.ExtendedStorage, metrics.Phase);
        Assert.AreEqual("#ff9800", metrics.PhaseColorHex);
        Assert.AreEqual(1.0, metrics.ViabilityPercentage);
    }

    [TestMethod]
    public void CalculateRecoveryMetrics_IdentifiesViabilityFadingPhase_WithSigmoidDecay()
    {
        const long now = 1_000_000;
        // 168 hours (7 days) ago
        var logs = new[] { Log(now - (168 * 3600), "Normal") };

        var metrics = _service.CalculateRecoveryMetrics(logs, now, minHours: 24, maxHours: 72);

        Assert.AreEqual(TelemetryPhase.ViabilityFading, metrics.Phase);
        Assert.AreEqual("#e53935", metrics.PhaseColorHex);
        Assert.IsTrue(metrics.ViabilityPercentage < 1.0);
        Assert.IsTrue(metrics.ViabilityPercentage >= 0.55); // Dynamic floor enforcement
    }

    // ==========================================
    // 74-DAY THERMAL SHADOW STAGE TESTS
    // ==========================================

    [TestMethod]
    public void GetThermalShadowDetails_IdentifiesStage1_EpididymalTransit()
    {
        const long now = 1_000_000;
        // Heat event 5 days ago
        var heat = Log(now - (5 * 86400), "Normal", heatFlag: 2);

        var details = _service.GetThermalShadowDetails(new[] { heat }, now);

        Assert.IsTrue(details.IsActive);
        Assert.AreEqual(5, details.DaysElapsed);
        Assert.AreEqual(69, details.DaysRemaining);
        StringAssert.Contains(details.StageName, "Epididymal Transit");
    }

    [TestMethod]
    public void GetThermalShadowDetails_IdentifiesStage2_Spermiogenesis()
    {
        const long now = 1_000_000;
        // Heat event 30 days ago
        var heat = Log(now - (30 * 86400), "Normal", heatFlag: 3);

        var details = _service.GetThermalShadowDetails(new[] { heat }, now);

        Assert.IsTrue(details.IsActive);
        Assert.AreEqual(30, details.DaysElapsed);
        StringAssert.Contains(details.StageName, "Spermiogenesis");
    }

    [TestMethod]
    public void GetThermalShadowDetails_IdentifiesStage3_MeioticRegeneration()
    {
        const long now = 1_000_000;
        // Heat event 60 days ago
        var heat = Log(now - (60 * 86400), "Normal", heatFlag: 2);

        var details = _service.GetThermalShadowDetails(new[] { heat }, now);

        Assert.IsTrue(details.IsActive);
        Assert.AreEqual(60, details.DaysElapsed);
        StringAssert.Contains(details.StageName, "Meiotic Regeneration");
    }

    [TestMethod]
    public void GetThermalShadowDetails_DeactivatesAfter74Days()
    {
        const long now = 1_000_000;
        // Heat event 75 days ago
        var heat = Log(now - (75 * 86400), "Normal", heatFlag: 2);

        var details = _service.GetThermalShadowDetails(new[] { heat }, now);

        Assert.IsFalse(details.IsActive);
    }

    [TestMethod]
    public void GetThermalShadowDetails_OverridesWhenPassingClinicalLabRecorded()
    {
        const long now = 1_000_000;
        var heat = Log(now - (10 * 86400), "Normal", heatFlag: 2);
        var lab = Log(now - (2 * 86400), "Normal", mode: "Clinical-Lab", concentration: 20, motility: 50);

        var details = _service.GetThermalShadowDetails(new[] { heat, lab }, now);

        Assert.IsFalse(details.IsActive);
    }

    // ==========================================
    // CLINICAL COMPLIANCE (WHO GUIDELINES) TESTS
    // ==========================================

    [TestMethod]
    public void CheckClinicalCompliance_IdealWindow_ReportsOnTrack()
    {
        const long now = 100_000;
        long appt = now + (24 * 3600); // Appt in 24 hours
        long lastRelease = now - (30 * 3600); // Last release 30h ago -> 54h total at appointment

        var compliance = _service.CheckClinicalCompliance(appt, lastRelease, now);

        Assert.IsTrue(compliance.HasAppointment);
        Assert.IsTrue(compliance.IsWithinWhoWindow);
        Assert.AreEqual("#4caf50", compliance.ComplianceColorHex); // Green
        StringAssert.Contains(compliance.ComplianceMessage, "Gold Standard");
    }

    [TestMethod]
    public void CheckClinicalCompliance_BelowMinimum_ReportsWarning()
    {
        const long now = 100_000;
        long appt = now + (12 * 3600); // Appt in 12 hours
        long lastRelease = now - (12 * 3600); // Last release 12h ago -> 24h total at appointment (<48h)

        var compliance = _service.CheckClinicalCompliance(appt, lastRelease, now);

        Assert.IsTrue(compliance.HasAppointment);
        Assert.IsFalse(compliance.IsWithinWhoWindow);
        Assert.AreEqual("#e53935", compliance.ComplianceColorHex); // Red
        StringAssert.Contains(compliance.ComplianceMessage, "<48h WHO minimum");
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
