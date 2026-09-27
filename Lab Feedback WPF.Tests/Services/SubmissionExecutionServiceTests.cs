using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class SubmissionExecutionServiceTests
{
    [TestMethod]
    public void SavedLocalPreference_DoesNotPromptOrAuthorizeVmFallback()
    {
        var settings = new Lab_Feedback_WPF.Models.LLMSettings
        {
            ExecutionMode = Lab_Feedback_WPF.Models.SubmissionExecutionMode.Local,
            ConfirmLocalExecution = false
        };
        Assert.IsTrue(SubmissionExecutionPolicy.IsLocalAuthorized(settings,
            _ => throw new AssertFailedException("Saved local preference must not prompt."), "solution.sln"));
        settings.ExecutionMode = Lab_Feedback_WPF.Models.SubmissionExecutionMode.HyperV;
        Assert.IsFalse(SubmissionExecutionPolicy.IsLocalAuthorized(settings, _ => true, "solution.sln"));
        var restored = System.Text.Json.JsonSerializer.Deserialize<Lab_Feedback_WPF.Models.LLMSettings>(
            System.Text.Json.JsonSerializer.Serialize(settings))!;
        Assert.IsFalse(restored.ConfirmLocalExecution);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunOnly_ResolvesExistingOutputWithoutBuilding(bool hasOutput)
    {
        var root = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var output = Path.Combine(root, "Game.exe");
            if (hasOutput) File.WriteAllText(output, "fixture");
            var builder = new SubmissionBuilder(
                (_, _, _, _, _, _) => throw new AssertFailedException("Run must not invoke a build process."),
                (_, _) => throw new AssertFailedException("Native Run must not resolve a compiler."));
            var result = await builder.ResolveExistingAsync(new RunnableSubmission
            {
                Kind = SubmissionKind.VisualStudioSolution, RootDirectory = root,
                EntryPath = Path.Combine(root, "Game.sln")
            }, CancellationToken.None);
            Assert.AreEqual(hasOutput, result.Succeeded);
            if (hasOutput) Assert.AreEqual(output, result.CommandFileName);
            else StringAssert.Contains(result.Log, "Run does not compile");
        }
        finally { Directory.Delete(root, true); }
    }

    [DataTestMethod]
    [DataRow(SubmissionKind.VisualStudioSolution)]
    [DataRow(SubmissionKind.CppProject)]
    public async Task NativeBuild_RunsFromProjectDirectoryForRelativeFiles(SubmissionKind kind)
    {
        var root = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", Guid.NewGuid().ToString("N"));
        var project = Path.Combine(root, "Game");
        var output = Path.Combine(project, "x64", "Debug");
        Directory.CreateDirectory(output);
        try
        {
            File.WriteAllText(Path.Combine(project, "Game.vcxproj"), "<Project />");
            File.WriteAllText(Path.Combine(project, "data.txt"), "required at startup");
            File.WriteAllText(Path.Combine(output, "Game.exe"), "build output fixture");
            var builder = new SubmissionBuilder(
                (_, _, _, _, _, _) => Task.FromResult(new ProcessRunResult { Started = true, ExitCode = 0 }),
                (name, _) => Task.FromResult<string?>(name));
            var result = await builder.BuildAsync(new RunnableSubmission
            {
                Kind = kind, RootDirectory = root,
                EntryPath = kind == SubmissionKind.CppProject
                    ? Path.Combine(project, "Game.vcxproj") : Path.Combine(root, "Game.sln")
            }, CancellationToken.None);
            Assert.IsTrue(result.Succeeded, result.Log);
            Assert.AreEqual(project, result.WorkingDirectory);
            Assert.IsTrue(File.Exists(Path.Combine(result.WorkingDirectory, "data.txt")));
            Assert.AreEqual(Path.Combine(output, "Game.exe"), result.CommandFileName);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void ManagedBuild_KeepsOutputWorkingDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", Guid.NewGuid().ToString("N"));
        var program = Path.Combine(root, "bin", "Game.dll");
        Assert.AreEqual(Path.GetDirectoryName(program), SubmissionBuilder.ResolveWorkingDirectory(
            new RunnableSubmission { Kind = SubmissionKind.DotNetProject, RootDirectory = root }, program));
    }

    [TestMethod]
    public async Task PublicEntryPoints_DefaultToIsolationWithoutLocalFallback()
    {
        var service = new SubmissionExecutionService(
            (_, _, _, _, _, _) => throw new AssertFailedException("Must not start a host process."),
            (_, _) => throw new AssertFailedException("Must not look up a host compiler."));
        // Verify the gate runs before filesystem traversal or staging, too.
        var path = "\0";
        var reports = new[]
        {
            await service.BuildOnlyAsync(path),
            await service.LaunchAsync(path),
            await service.ExecuteAndFormatAsync(path, null, "", null, null,
                new Lab_Feedback_WPF.Models.LLMSettings { ExecuteStudentSubmissions = true })
        };
        foreach (var report in reports)
            StringAssert.Contains(report, SubmissionExecutionPolicy.NoRuntimeDeduction);
    }

    [TestMethod]
    public async Task LocalExecution_DecliningWarnsOnEveryOperationAndStopsBeforeReadingFiles()
    {
        var settings = new Lab_Feedback_WPF.Models.LLMSettings
        { ExecutionMode = Lab_Feedback_WPF.Models.SubmissionExecutionMode.Local };
        var warnings = new List<string>();
        var service = new SubmissionExecutionService(settings, warning => { warnings.Add(warning); return false; });
        Assert.AreEqual(SubmissionExecutionPolicy.LocalDeclined, await service.BuildOnlyAsync("\0"));
        Assert.AreEqual(SubmissionExecutionPolicy.LocalDeclined, await service.LaunchAsync("\0"));
        Assert.AreEqual(SubmissionExecutionPolicy.LocalDeclined,
            await service.ExecuteAndFormatAsync("\0", null, "", null, null, settings));
        Assert.AreEqual(3, warnings.Count);
        Assert.IsTrue(warnings.All(w => w.Contains("Windows permissions")));
    }

    [TestMethod]
    public async Task LocalExecution_WithoutConsentCallbackIsBlocked()
    {
        var settings = new Lab_Feedback_WPF.Models.LLMSettings
        { ExecutionMode = Lab_Feedback_WPF.Models.SubmissionExecutionMode.Local };
        Assert.AreEqual(SubmissionExecutionPolicy.LocalDeclined,
            await new SubmissionExecutionService(settings).BuildOnlyAsync("\0"));
    }

    [TestMethod]
    public async Task LocalExecution_AcceptingWarningReachesLocalPath()
    {
        var settings = new Lab_Feedback_WPF.Models.LLMSettings
        { ExecutionMode = Lab_Feedback_WPF.Models.SubmissionExecutionMode.Local };
        // Path validation proves the accepted operation reached the local backend,
        // without building or running an actual submission on the test host.
        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            new SubmissionExecutionService(settings, _ => true).BuildOnlyAsync("\0"));
    }

    [TestMethod]
    public void BuildMsBuildArguments_RequestsRebuildFromStagedCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sln = Path.Combine(root, "Game.sln");
        File.WriteAllText(sln, "sln");
        var args = SubmissionBuilder.BuildMsBuildArguments(new RunnableSubmission
        {
            Kind = SubmissionKind.VisualStudioSolution,
            RootDirectory = root,
            EntryPath = sln
        });

        StringAssert.Contains(args, "/t:Rebuild");
        StringAssert.Contains(args, sln);
    }

    [TestMethod]
    [DataRow(SubmissionKind.VisualStudioSolution)]
    [DataRow(SubmissionKind.CppProject)]
    [DataRow(SubmissionKind.DotNetProject)]
    public async Task FailedBuild_DoesNotRunStaleExecutable(SubmissionKind kind)
    {
        await CheckRejectedBuild(kind, toolsAvailable: true);
    }

    [TestMethod]
    [DataRow(SubmissionKind.VisualStudioSolution)]
    [DataRow(SubmissionKind.CppSources)]
    [DataRow(SubmissionKind.NativeExecutable)]
    public async Task MissingCompiler_DoesNotUsePrebuiltExecutable(SubmissionKind kind)
    {
        await CheckRejectedBuild(kind, toolsAvailable: false);
    }

    private static async Task CheckRejectedBuild(SubmissionKind kind, bool toolsAvailable)
    {
        var root = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "Game.exe"), "old executable");
            var service = new SubmissionExecutionService(
                (_, _, _, _, _, _) => Task.FromResult(new ProcessRunResult
                {
                    Started = true, ExitCode = 1, StandardError = "compiler rejected source"
                }),
                (name, _) => Task.FromResult<string?>(toolsAvailable ? name : null));
            var result = await service.BuildAsync(new RunnableSubmission
            {
                Kind = kind, RootDirectory = root, EntryPath = Path.Combine(root, "Game.sln")
            }, CancellationToken.None);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(string.Empty, result.CommandFileName);
            if (toolsAvailable)
                StringAssert.Contains(result.Log, "compiler rejected source");
        }
        finally { Directory.Delete(root, true); }
    }
}
