using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class NativeBuildHandlingTests
{
    [TestMethod]
    public void LaunchUsesSolutionDisplayNameToFindWorkingDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Practice"));
        Directory.CreateDirectory(Path.Combine(root, "CaveMatchingGame"));
        try
        {
            File.WriteAllText(Path.Combine(root, "Practice", "Practice.vcxproj"), "<Project />");
            File.WriteAllText(Path.Combine(root, "CaveMatchingGame", "CaveMatchingGame.vcxproj"), "<Project />");
            var solution = Path.Combine(root, "Week1.sln");
            File.WriteAllText(solution, """
                Project("{TYPE}") = "Lectures", "Practice\Practice.vcxproj", "{FIRST}"
                EndProject
                Project("{TYPE}") = "Lab1", "CaveMatchingGame\CaveMatchingGame.vcxproj", "{SECOND}"
                EndProject
                """);
            var submission = new RunnableSubmission { Kind = SubmissionKind.VisualStudioSolution, EntryPath = solution, RootDirectory = root };
            Assert.AreEqual(Path.Combine(root, "CaveMatchingGame"), SubmissionBuilder.ResolveWorkingDirectory(submission,
                Path.Combine(root, "Practice", "x64", "Debug", "Lab1.exe")));
            Assert.AreEqual(Path.Combine(root, "Practice"), SubmissionBuilder.ResolveWorkingDirectory(submission,
                Path.Combine(root, "Practice", "x64", "Debug", "Lectures.exe")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task SolutionBuildPreservesEachProjectsIntermediateDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var name in new[] { "First", "Second" })
            {
                var folder = Path.Combine(root, name);
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, name + ".vcxproj"), """
                    <Project>
                      <PropertyGroup><IntDir Condition="'$(IntDir)' == ''">$(MSBuildProjectDirectory)\obj\</IntDir></PropertyGroup>
                      <Target Name="Rebuild">
                        <Error Condition="'$(IntDir)' != '$(MSBuildProjectDirectory)\obj\'" Text="Intermediate directory was overridden across projects" />
                        <MakeDir Directories="$(IntDir)" />
                        <WriteLinesToFile File="$(IntDir)compiler.pdb" Lines="fixture" Overwrite="true" />
                      </Target>
                    </Project>
                    """);
            }
            var entry = Path.Combine(root, "Build.proj");
            File.WriteAllText(entry, """
                <Project><Target Name="Rebuild">
                  <MSBuild Projects="First\First.vcxproj;Second\Second.vcxproj" Targets="Rebuild" />
                </Target></Project>
                """);
            var args = SubmissionBuilder.BuildMsBuildArguments(new RunnableSubmission
                { Kind = SubmissionKind.VisualStudioSolution, RootDirectory = root, EntryPath = entry });
            StringAssert.Contains(args, "/m:1");
            var result = await ProcessRunner.RunAsync("dotnet", "msbuild " + args, root, null, TimeSpan.FromSeconds(20));
            Assert.AreEqual(0, result.ExitCode, result.StandardOutput + result.StandardError);
            Assert.IsTrue(File.Exists(Path.Combine(root, "First", "obj", "compiler.pdb")));
            Assert.IsTrue(File.Exists(Path.Combine(root, "Second", "obj", "compiler.pdb")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task OutputDirectory_WithSpaces_AllowsUnattendedPostBuildCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", Guid.NewGuid().ToString("N"), "Project with spaces");
        Directory.CreateDirectory(root);
        try
        {
            var project = Path.Combine(root, "Game.vcxproj");
            File.WriteAllText(Path.Combine(root, "dependency.dll"), "test fixture");
            File.WriteAllText(project, """
                <Project>
                  <Target Name="Rebuild">
                    <MakeDir Directories="$(OutDir)" />
                    <Exec Command="xcopy /y /d &quot;dependency.dll&quot; &quot;$(OutDir)&quot;" />
                  </Target>
                </Project>
                """);
            var args = SubmissionBuilder.BuildMsBuildArguments(new RunnableSubmission
            {
                Kind = SubmissionKind.VisualStudioSolution, RootDirectory = root, EntryPath = project
            });
            var result = await ProcessRunner.RunAsync("dotnet", "msbuild " + args, root, null, TimeSpan.FromSeconds(20));
            Assert.IsFalse(result.TimedOut, result.StandardOutput);
            Assert.AreEqual(0, result.ExitCode, result.StandardOutput + result.StandardError + result.Error);
            var output = RunnableSubmissionDetector.GetNativeOutDir(root, RunnableSubmissionDetector.InferNativePlatform(root));
            Assert.IsTrue(File.Exists(Path.Combine(output, "dependency.dll")), result.StandardOutput);
            Assert.IsFalse(result.StandardOutput.Contains("(F = file, D = directory)"));
        }
        finally { Directory.Delete(root, true); }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NativeFailure_IsReportedWithoutRetryingOrLaunchingStaleOutput(bool timedOut)
    {
        var root = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "Game.vcxproj"), "<Project />");
            File.WriteAllText(Path.Combine(root, "Game.exe"), "stale output");
            var calls = 0;
            var builder = new SubmissionBuilder((_, _, _, _, _, _) =>
            {
                calls++;
                return Task.FromResult(new ProcessRunResult
                {
                    Started = true, TimedOut = timedOut, ExitCode = 1,
                    StandardOutput = "(F = file, D = directory)\nLINK : fatal error LNK1104: cannot open file 'Game.exe'", Error = timedOut ? "Timed out after 90s." : "Copy failed."
                });
            }, (name, _) => Task.FromResult<string?>(name));
            var result = await builder.BuildAsync(new RunnableSubmission
            {
                Kind = SubmissionKind.VisualStudioSolution, RootDirectory = root, EntryPath = Path.Combine(root, "Game.sln")
            }, CancellationToken.None);
            Assert.AreEqual(1, calls);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("", result.CommandFileName);
            StringAssert.Contains(result.Log, "post-build copy step");
            StringAssert.Contains(result.Log, "previously launched submission");
            if (timedOut) StringAssert.Contains(result.Log, "Build stopped after the time limit");
        }
        finally { Directory.Delete(root, true); }
    }
}
