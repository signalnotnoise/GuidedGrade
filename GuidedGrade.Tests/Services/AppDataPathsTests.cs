using GuidedGrade.Models;
using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class AppDataPathsTests
{
    [TestMethod]
    public void LegacyDirectoryMovesToGuidedGradeDirectory()
    {
        var root = CreateRoot();
        try
        {
            var legacy = Path.Combine(root, AppDataPaths.LegacyProductDirectoryName);
            Directory.CreateDirectory(legacy);
            File.WriteAllText(Path.Combine(legacy, "llm-settings.json"), "settings");

            var current = AppDataPaths.EnsureProductDirectory(root);

            Assert.AreEqual(Path.Combine(root, AppDataPaths.ProductDirectoryName), current);
            Assert.AreEqual("settings", File.ReadAllText(Path.Combine(current, "llm-settings.json")));
            Assert.IsFalse(Directory.Exists(legacy));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ExistingGuidedGradeDataWinsWhileMissingLegacyFilesMove()
    {
        var root = CreateRoot();
        try
        {
            var legacy = Path.Combine(root, AppDataPaths.LegacyProductDirectoryName);
            var current = Path.Combine(root, AppDataPaths.ProductDirectoryName);
            Directory.CreateDirectory(legacy);
            Directory.CreateDirectory(current);
            File.WriteAllText(Path.Combine(legacy, "assignments.db"), "legacy");
            File.WriteAllText(Path.Combine(legacy, "section-comments.db"), "comments");
            File.WriteAllText(Path.Combine(current, "assignments.db"), "current");

            AppDataPaths.EnsureProductDirectory(root);

            Assert.AreEqual("current", File.ReadAllText(Path.Combine(current, "assignments.db")));
            Assert.AreEqual("comments", File.ReadAllText(Path.Combine(current, "section-comments.db")));
            Assert.AreEqual("legacy", File.ReadAllText(Path.Combine(legacy, "assignments.db")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void SavedRunnerPathsFollowMigratedLocalData()
    {
        var root = CreateRoot();
        try
        {
            var legacy = Path.Combine(root, AppDataPaths.LegacyProductDirectoryName);
            var credential = Path.Combine(legacy, "runner-guest.xml");
            var worker = Path.Combine(legacy, "Worker");
            Directory.CreateDirectory(worker);
            File.WriteAllText(credential, "credential");
            File.WriteAllText(Path.Combine(worker, "GuidedGrade.Runner.exe"), "runner");
            var settings = new LLMSettings
            {
                RunnerCredentialFile = credential,
                RunnerWorkerFolder = worker
            };

            Assert.IsTrue(settings.MigrateLegacyLocalPaths(root));

            var current = Path.Combine(root, AppDataPaths.ProductDirectoryName);
            Assert.AreEqual(Path.Combine(current, "runner-guest.xml"), settings.RunnerCredentialFile);
            Assert.AreEqual(Path.Combine(current, "Worker"), settings.RunnerWorkerFolder);
            Assert.IsTrue(File.Exists(settings.RunnerCredentialFile));
            Assert.IsTrue(File.Exists(Path.Combine(settings.RunnerWorkerFolder, "GuidedGrade.Runner.exe")));
            Assert.IsFalse(settings.MigrateLegacyLocalPaths(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void SpacedLegacyDiagnosticsMigrateWithOtherProductDirectories(bool hasLegacyData, bool hasCurrentData)
    {
        var root = CreateRoot();
        try
        {
            var legacy = Path.Combine(root, AppDataPaths.LegacyProductDirectoryName);
            var diagnosticsRoot = Path.Combine(root, AppDataPaths.LegacyDiagnosticsDirectoryName);
            var current = Path.Combine(root, AppDataPaths.ProductDirectoryName);
            Directory.CreateDirectory(Path.Combine(diagnosticsRoot, "Diagnostics"));
            File.WriteAllText(Path.Combine(diagnosticsRoot, "Diagnostics", "com-cleanup.log"), "old diagnostic");
            if (hasLegacyData)
            {
                Directory.CreateDirectory(legacy);
                File.WriteAllText(Path.Combine(legacy, "llm-settings.json"), "settings");
            }
            if (hasCurrentData)
            {
                Directory.CreateDirectory(current);
                File.WriteAllText(Path.Combine(current, "assignments.db"), "current data");
            }

            Assert.AreEqual(current, AppDataPaths.EnsureProductDirectory(root));
            Assert.AreEqual(current, AppDataPaths.EnsureProductDirectory(root));

            Assert.AreEqual("old diagnostic", File.ReadAllText(Path.Combine(current, "Diagnostics", "com-cleanup.log")));
            Assert.IsFalse(Directory.Exists(diagnosticsRoot));
            Assert.IsFalse(Directory.Exists(legacy));
            if (hasLegacyData)
                Assert.AreEqual("settings", File.ReadAllText(Path.Combine(current, "llm-settings.json")));
            if (hasCurrentData)
                Assert.AreEqual("current data", File.ReadAllText(Path.Combine(current, "assignments.db")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConflictingDiagnosticsRemainInLegacyDirectories(bool hasCurrentLog)
    {
        var root = CreateRoot();
        try
        {
            var legacy = Path.Combine(root, AppDataPaths.LegacyProductDirectoryName, "Diagnostics");
            var spacedLegacy = Path.Combine(root, AppDataPaths.LegacyDiagnosticsDirectoryName, "Diagnostics");
            var current = Path.Combine(root, AppDataPaths.ProductDirectoryName, "Diagnostics");
            Directory.CreateDirectory(legacy);
            Directory.CreateDirectory(spacedLegacy);
            File.WriteAllText(Path.Combine(legacy, "com-cleanup.log"), "unspaced log");
            File.WriteAllText(Path.Combine(spacedLegacy, "com-cleanup.log"), "spaced log");
            File.WriteAllText(Path.Combine(spacedLegacy, "older.log"), "older log");
            if (hasCurrentLog)
            {
                Directory.CreateDirectory(current);
                File.WriteAllText(Path.Combine(current, "com-cleanup.log"), "current log");
            }

            AppDataPaths.EnsureProductDirectory(root);
            AppDataPaths.EnsureProductDirectory(root);

            Assert.AreEqual(hasCurrentLog ? "current log" : "unspaced log",
                File.ReadAllText(Path.Combine(current, "com-cleanup.log")));
            Assert.AreEqual("spaced log", File.ReadAllText(Path.Combine(spacedLegacy, "com-cleanup.log")));
            Assert.AreEqual("older log", File.ReadAllText(Path.Combine(current, "older.log")));
            Assert.IsFalse(File.Exists(Path.Combine(spacedLegacy, "older.log")));
            if (hasCurrentLog)
                Assert.AreEqual("unspaced log", File.ReadAllText(Path.Combine(legacy, "com-cleanup.log")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "GuidedGrade-AppDataPaths-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
