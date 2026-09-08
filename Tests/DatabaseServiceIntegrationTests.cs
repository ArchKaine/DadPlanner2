using System;
using System.IO;
using System.Linq;
using DadPlanner2.Models;
using DadPlanner2.Services;
using Microsoft.Data.Sqlite;
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
    public void LegacyRows_DefaultReleaseCountAndUnknownConfidence()
    {
        string databasePath = Path.Combine(_testDirectory, "inventory.db");
        Directory.CreateDirectory(_testDirectory);
        using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Logs (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp INTEGER); INSERT INTO Logs (Timestamp) VALUES (1000);";
            command.ExecuteNonQuery();
        }

        var service = new DatabaseService(_testDirectory);
        var saved = service.GetAllLogs().Single();

        Assert.AreEqual(1, saved.ReleaseCount);
        Assert.AreEqual(VolumeConfidence.Unknown, saved.VolumeConfidence);
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
            ReleaseCount = 3,
            VolumeConfidence = VolumeConfidence.Observed,
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
        Assert.AreEqual(log.ReleaseCount, saved.ReleaseCount);
        Assert.AreEqual(log.VolumeConfidence, saved.VolumeConfidence);
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

    [TestMethod]
    public void TestMode_SeedsMultiReleaseSessionsWithConfidenceValues()
    {
        var service = new DatabaseService(_testDirectory);
        service.ToggleTestMode();

        var logs = service.GetAllLogs();

        Assert.IsTrue(logs.Count >= 150);
        Assert.IsTrue(logs.Any(log => log.ReleaseCount > 1));
        Assert.IsTrue(logs.Any(log => log.ReleaseCount > 1 && log.VolumeConfidence == VolumeConfidence.Estimated));
        Assert.IsTrue(logs.Any(log => log.ReleaseCount > 1 && log.VolumeConfidence == VolumeConfidence.Unknown));
        Assert.IsTrue(logs.Any(log => log.ReleaseCount == 1 && log.VolumeConfidence == VolumeConfidence.Observed));
    }

    [TestMethod]
    public void ExecuteAutoBackup_CreatesReadableBackupAndRetainsTen()
    {
        var service = new DatabaseService(_testDirectory);
        service.InsertLog(new LogRecord { Timestamp = 1000, Volume = "Normal" });
        string backupDirectory = Path.Combine(_testDirectory, "backups");

        for (int i = 0; i < 11; i++)
        {
            service.MarkDirty();
            service.ExecuteAutoBackup(backupDirectory);
            File.SetLastWriteTimeUtc(
                Directory.GetFiles(backupDirectory, "inventory-*.db").OrderByDescending(File.GetLastWriteTimeUtc).First(),
                DateTime.UtcNow.AddMinutes(-i));
        }

        Assert.AreEqual(10, Directory.GetFiles(backupDirectory, "inventory-*.db").Length);
        Assert.IsNotNull(service.GetLatestBackup(backupDirectory));
    }

    [TestMethod]
    public void RestoreBackup_CreatesPreRestoreSafetySnapshot()
    {
        var service = new DatabaseService(_testDirectory);
        service.InsertLog(new LogRecord { Timestamp = 1000, Volume = "Normal" });
        string backupDirectory = Path.Combine(_testDirectory, "backups");
        service.MarkDirty();
        service.ExecuteAutoBackup(backupDirectory);
        string backupPath = service.GetLatestBackup(backupDirectory)!;

        service.RestoreBackup(backupPath);

        Assert.IsTrue(Directory.GetFiles(_testDirectory, "inventory.db.pre-restore-*.db").Length == 1);
    }

    [TestMethod]
    public void RestoreBackup_RejectsCorruptBackup()
    {
        var service = new DatabaseService(_testDirectory);
        string corruptPath = Path.Combine(_testDirectory, "corrupt.db");
        File.WriteAllText(corruptPath, "not a sqlite database");

        Assert.ThrowsException<SqliteException>(() => service.RestoreBackup(corruptPath));
    }

    [TestMethod]
    public void ExportData_WritesJsonAndCsvRecords()
    {
        var service = new DatabaseService(_testDirectory);
        service.InsertLog(new LogRecord
        {
            Timestamp = 1000,
            Mode = "Playtime",
            Volume = "High",
            ReleaseCount = 2,
            VolumeConfidence = VolumeConfidence.Estimated,
            Supplements = "{\"zinc\":1}"
        });
        string exportDirectory = Path.Combine(_testDirectory, "exports");

        var exports = service.ExportData(exportDirectory);

        StringAssert.Contains(File.ReadAllText(exports.JsonPath), "\"Mode\": \"Playtime\"");
        StringAssert.Contains(File.ReadAllText(exports.JsonPath), "\"VolumeConfidence\": \"Estimated\"");
        StringAssert.Contains(File.ReadAllText(exports.CsvPath), "\"Playtime\"");
        StringAssert.Contains(File.ReadAllText(exports.CsvPath), "ReleaseCount,VolumeConfidence");
        StringAssert.Contains(File.ReadAllText(exports.CsvPath), "2,\"Estimated\"");
        StringAssert.Contains(File.ReadAllText(exports.CsvPath), "\"{\"\"zinc\"\":1}\"");
        Assert.IsNotNull(exports.AnalysisJsonPath);
        Assert.IsNotNull(exports.AnalysisCsvPath);
        Assert.IsTrue(File.Exists(exports.AnalysisJsonPath));
        Assert.IsTrue(File.Exists(exports.AnalysisCsvPath));
        StringAssert.Contains(File.ReadAllText(exports.AnalysisCsvPath), "Supplement,WindowDays,TargetTimestamp");
    }

    [TestMethod]
    public void ExportData_CsvEscapesCommasQuotesAndNewlines()
    {
        var service = new DatabaseService(_testDirectory);
        service.InsertLog(new LogRecord
        {
            Timestamp = 1000,
            Mode = "Clinical, \"Lab\"\nReview",
            Volume = "Normal",
            Supplements = "{\"note\":\"dose, daily\"}"
        });
        string exportDirectory = Path.Combine(_testDirectory, "exports");

        var exports = service.ExportData(exportDirectory);
        string csv = File.ReadAllText(exports.CsvPath);

        StringAssert.Contains(csv, "\"Clinical, \"\"Lab\"\"\nReview\"");
        StringAssert.Contains(csv, "\"{\"\"note\"\":\"\"dose, daily\"\"}\"");
    }
}
