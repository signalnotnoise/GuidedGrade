using System.IO.Compression;
using System.Text.Json;
using GuidedGrade.Models;
using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class HyperVRunnerTests
{
    [TestMethod]
    public void Packaging_RejectsGuestCredentialsInsideInputs()
    {
        var root = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", "input");
        var settings = new LLMSettings
        {
            RunnerWorkerFolder = Path.Combine(root, "worker"),
            RunnerCredentialFile = Path.Combine(root, "guest-credential.xml"),
            RunnerBaseDisk = Path.Combine(Path.GetTempPath(), "template.vhdx")
        };
        Assert.ThrowsException<IOException>(() => HyperVRunner.ValidateInputSeparation(settings, root));
    }

    [TestMethod]
    public void ExistingSettings_DefaultToHyperV()
    {
        var settings = JsonSerializer.Deserialize<LLMSettings>("{\"ExecuteStudentSubmissions\":true}")!;
        Assert.AreEqual(SubmissionExecutionMode.HyperV, settings.ExecutionMode);
        Assert.IsNotNull(HyperVRunner.ConfigurationError(settings));
    }

    [TestMethod]
    public void InvalidExecutionMode_DoesNotAuthorizeLocalRun()
    {
        Assert.IsFalse(SubmissionExecutionPolicy.IsLocalAuthorized((SubmissionExecutionMode)42,
            _ => throw new AssertFailedException("Unknown modes must not ask to run locally."), "test"));
    }

    [TestMethod]
    public async Task Protocol_RejectsUnboundedGuestOutput()
    {
        using var reader = new StringReader(new string('x', 1048577));
        await Assert.ThrowsExceptionAsync<IOException>(() => HyperVRunner.ReadBoundedLineAsync(reader, CancellationToken.None));
    }

    [TestMethod]
    public async Task Protocol_PreservesJsonAndHandlesEof()
    {
        using var reader = new StringReader("{\"text\":\"hello\"}\r\n");
        Assert.AreEqual("{\"text\":\"hello\"}", await HyperVRunner.ReadBoundedLineAsync(reader, CancellationToken.None));
        Assert.IsNull(await HyperVRunner.ReadBoundedLineAsync(reader, CancellationToken.None));
    }

    [TestMethod]
    public void Package_CopiesSubmissionWithoutGitMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "source", ".git"));
        try
        {
            File.WriteAllText(Path.Combine(root, "source", "Game.slnx"), "<Solution/>");
            File.WriteAllText(Path.Combine(root, "source", ".git", "config"), "private metadata");
            var archive = Path.Combine(root, "submission.zip");
            HyperVRunner.PackageDirectory(Path.Combine(root, "source"), archive, CancellationToken.None);
            using var zip = ZipFile.OpenRead(archive);
            Assert.AreEqual(1, zip.Entries.Count);
            Assert.AreEqual("Game.slnx", zip.Entries[0].FullName);
        }
        finally { Directory.Delete(root, true); }
    }
}
