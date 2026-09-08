using System;
using System.IO;
using DadPlanner2.Models;
using DadPlanner2.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DadPlanner2.Tests;

[TestClass]
public sealed class ReportDocumentServiceTests
{
    private string _testDirectory = null!;

    [TestInitialize]
    public void SetUp()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"dadplanner-report-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [TestMethod]
    public void Generate_WritesPdfToRequestedPath()
    {
        string outputPath = Path.Combine(_testDirectory, "Baseline_Summary.pdf");
        var reportData = new ReportDataService().Create(Array.Empty<LogRecord>(), 1_700_000_000);

        new ReportDocumentService().Generate(reportData, outputPath);

        Assert.IsTrue(File.Exists(outputPath));
        Assert.IsTrue(new FileInfo(outputPath).Length > 0);
        CollectionAssert.AreEqual("%PDF-"u8.ToArray(), File.ReadAllBytes(outputPath)[..5]);
    }
}
