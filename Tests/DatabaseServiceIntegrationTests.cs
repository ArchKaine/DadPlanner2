using System;
using System.IO;
using System.Linq;
using DadPlanner2.Models;
using DadPlanner2.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DadPlanner2.Tests;

[TestClass]
public sealed class DatabaseServiceIntegrationTests
{
    private string _testDirectory = null!;

    [TestInitialize]
    public void SetUp()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"dadplanner-tests-{Guid.NewGuid():N}");
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [TestMethod]
    public void NewDatabase_CreatesExpectedDefaults()
    {
        var service = new DatabaseService(_testDirectory);

        Assert.AreEqual((24, 72), service.GetThresholdSettings());
        Assert.AreEqual(0L, service.GetAppointment());
        Assert.IsEmpty(service.GetAllLogs());
    }

    [TestMethod]
    public void LogRoundTrip_PreservesClinicalFieldsAndPdfMetadata()
    {
        var service = new DatabaseService(_testDirectory);
        var log = new LogRecord
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60,
            Mode = "Clinical-Lab",
            Volume = "Normal",
            HeatFlag = 1,
            Supplements = "{\"zinc\":1}",
            ClinicalVol = 2.4,
            Concentration = 42,
            Motility = 61,
            ProgMotility = 48,
            Morphology = 6,
            PhLevel = 7.6
        };

        service.InsertLog(log, "report.pdf", new byte[] { 1, 2, 3 });
        var saved = service.GetAllLogs().Single();

        Assert.AreEqual(log.Mode, saved.Mode);
        Assert.AreEqual(log.ClinicalVol, saved.ClinicalVol);
        Assert.AreEqual(log.Concentration, saved.Concentration);
        Assert.AreEqual(log.ProgMotility, saved.ProgMotility);
        Assert.IsTrue(saved.HasPdf);
    }

    [TestMethod]
    public void SettingsAndAppointment_RoundTrip()
    {
        var service = new DatabaseService(_testDirectory);
        service.SaveThresholdSettings(18.5, 80.25);
        service.SaveSupplementsState(true, false, true, false);
        service.SetAppointment(1_700_000_000);

        Assert.AreEqual((18.5, 80.25), service.GetThresholdSettings());
        Assert.AreEqual((true, false, true, false), service.GetSupplementsState());
        Assert.AreEqual(1_700_000_000L, service.GetAppointment());
    }
}
