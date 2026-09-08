using System.Linq;
using DadPlanner2.Models;
using DadPlanner2.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DadPlanner2.Tests;

[TestClass]
public sealed class SupplementSaturationServiceTests
{
    [TestMethod]
    public void Calculate_UsesReleaseEventsOnlyAndIncludesBoundaries()
    {
        var logs = new[]
        {
            Log(0, "Normal", true),
            Log(10, "None", true),
            Log(100, "Normal", false),
            Log(200, "Normal", true),
            Log(201, "Normal", true)
        };

        var result = new SupplementSaturationService().Calculate(logs, 200, "zinc", 0);

        Assert.AreEqual(1, result.ReleaseEventCount);
        Assert.AreEqual(1, result.SupplementEventCount);
        Assert.AreEqual(1.0, result.SupplementProportion);
        Assert.IsTrue(result.IsSaturated);
    }

    [TestMethod]
    public void Calculate_ExactlyHalfIsSaturated()
    {
        var logs = Enumerable.Range(0, 4)
            .Select(index => Log(index, "Normal", index < 2))
            .ToArray();

        var result = new SupplementSaturationService().Calculate(logs, 3, "zinc", 1);

        Assert.AreEqual(4, result.ReleaseEventCount);
        Assert.AreEqual(2, result.SupplementEventCount);
        Assert.AreEqual(0.5, result.SupplementProportion);
        Assert.IsTrue(result.IsSaturated);
    }

    [TestMethod]
    public void Calculate_EmptyWindowIsNotSaturated()
    {
        var result = new SupplementSaturationService()
            .Calculate(new[] { Log(100, "Normal", true) }, 0, "zinc", 1);

        Assert.AreEqual(0, result.ReleaseEventCount);
        Assert.AreEqual(0, result.SupplementEventCount);
        Assert.AreEqual(0, result.SupplementProportion);
        Assert.IsFalse(result.IsSaturated);
    }

    private static LogRecord Log(long timestamp, string volume, bool zinc) =>
        new()
        {
            Timestamp = timestamp,
            Volume = volume,
            Supplements = $"{{\"zinc\":{(zinc ? 1 : 0)}}}"
        };
}
