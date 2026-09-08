using System;
using DadPlanner2.Models;
using DadPlanner2.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DadPlanner2.Tests;

[TestClass]
public sealed class LogValidationServiceTests
{
    private readonly LogValidationService _service = new();

    [TestMethod]
    public void ValidateThresholds_RejectsCeilingAtOrBelowFloor()
    {
        Assert.IsNotNull(_service.ValidateThresholds(24, 24));
        Assert.IsNotNull(_service.ValidateThresholds(72, 24));
    }

    [TestMethod]
    public void ValidateThresholds_RejectsNonFiniteValues()
    {
        Assert.IsNotNull(_service.ValidateThresholds(double.NaN, 72));
        Assert.IsNotNull(_service.ValidateThresholds(24, double.PositiveInfinity));
    }

    [TestMethod]
    public void ValidateThresholds_AllowsValidValues()
    {
        Assert.IsNull(_service.ValidateThresholds(24, 72));
    }

    [TestMethod]
    public void Validate_RejectsDuplicateTimestampButAllowsCurrentRecord()
    {
        var existing = new[] { new LogRecord { Id = 7, Timestamp = 1000 } };

        Assert.IsNotNull(_service.Validate(existing, "Maintenance", null, null, null, null, null, null, 1000));
        Assert.IsNull(_service.Validate(existing, "Maintenance", null, null, null, null, null, null, 1000, 7));
    }

    [TestMethod]
    public void Validate_RejectsProgressiveMotilityAboveTotal()
    {
        var error = _service.Validate(
            Array.Empty<LogRecord>(), "Clinical-Lab", 2.0, 15, 40, 41, 5, 7.5, 1000);

        Assert.AreEqual("Progressive motility must be between 0 and total motility.", error);
    }

    [TestMethod]
    public void Validate_AllowsValidClinicalRecord()
    {
        var error = _service.Validate(
            Array.Empty<LogRecord>(), "Clinical-Lab", 2.0, 15, 40, 35, 5, 7.5, 1000);

        Assert.IsNull(error);
    }

    [TestMethod]
    public void Validate_RequiresPositiveReleaseCount()
    {
        var error = _service.Validate(
            Array.Empty<LogRecord>(), "Maintenance", null, null, null, null, null, null, 1000,
            releaseCount: 0);

        Assert.AreEqual("Release count must be a positive whole number.", error);
    }

    [TestMethod]
    public void Validate_RejectsInvalidVolumeConfidence()
    {
        var error = _service.Validate(
            Array.Empty<LogRecord>(), "Maintenance", null, null, null, null, null, null, 1000,
            volumeConfidence: (VolumeConfidence)99);

        Assert.AreEqual("Volume confidence must be Observed, Estimated, or Unknown.", error);
    }

    [TestMethod]
    public void Validate_AllowsReleaseCountAndConfidence()
    {
        var error = _service.Validate(
            Array.Empty<LogRecord>(), "Maintenance", null, null, null, null, null, null, 1000,
            releaseCount: 2, volumeConfidence: VolumeConfidence.Observed);

        Assert.IsNull(error);
    }
}
