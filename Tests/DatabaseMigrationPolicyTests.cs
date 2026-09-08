using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DadPlanner2.Services;

namespace DadPlanner2.Tests;

[TestClass]
public sealed class DatabaseMigrationPolicyTests
{
    [TestMethod]
    public void IsAlreadyApplied_RecognizesDuplicateColumnErrors()
    {
        var exception = new SqliteException("duplicate column name: Mode", 1);

        Assert.IsTrue(DatabaseMigrationPolicy.IsAlreadyApplied(exception));
    }

    [TestMethod]
    public void IsAlreadyApplied_DoesNotIgnoreOtherSqliteErrors()
    {
        var exception = new SqliteException("no such table: Logs", 1);

        Assert.IsFalse(DatabaseMigrationPolicy.IsAlreadyApplied(exception));
    }
}
