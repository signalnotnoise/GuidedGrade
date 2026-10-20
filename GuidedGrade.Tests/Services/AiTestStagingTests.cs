using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class AiTestStagingTests
{
    [TestMethod]
    public async Task StageSharedDependencyPreservesImportsWithoutCopyingOtherRepositoryFolders()
    {
        var fixture = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", Guid.NewGuid().ToString("N"));
        var week = Path.Combine(fixture, "repo", "Week1");
        var shared = Path.Combine(fixture, "repo", "Shared");
        Directory.CreateDirectory(Path.Combine(week, "Practice"));
        Directory.CreateDirectory(Path.Combine(shared, "PropertySheets"));
        Directory.CreateDirectory(Path.Combine(shared, "bin"));
        Directory.CreateDirectory(Path.Combine(fixture, "repo", "Private"));
        try
        {
            File.WriteAllText(Path.Combine(fixture, "repo", "Private", "notes.txt"), "unrelated");
            File.WriteAllText(Path.Combine(shared, "bin", "runtime.dll"), "dependency");
            File.WriteAllText(Path.Combine(shared, "bin", "stale.exe"), "excluded");
            File.WriteAllText(Path.Combine(shared, "PropertySheets", "Shared.props"), "<Project><PropertyGroup><FoundShared>true</FoundShared></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(week, "Practice", "Practice.vcxproj"), """
                <Project><Import Project="..\..\Shared\PropertySheets\Shared.props" />
                  <Target Name="Check"><Error Condition="'$(FoundShared)' != 'true'" Text="Missing dependency" /></Target>
                </Project>
                """);
            var staged = AiTestStaging.StageWithSharedDependencies(week, Path.Combine(fixture, "staging"));
            var run = Path.GetDirectoryName(staged)!;
            Assert.IsTrue(File.Exists(Path.Combine(run, "Shared", "bin", "runtime.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(run, "Shared", "bin", "stale.exe")));
            Assert.IsFalse(Directory.Exists(Path.Combine(run, "Private")));
            var result = await ProcessRunner.RunAsync("dotnet", $"msbuild \"{Path.Combine(staged, "Practice", "Practice.vcxproj")}\" /t:Check /nologo", staged, null, TimeSpan.FromSeconds(20));
            Assert.AreEqual(0, result.ExitCode, result.StandardOutput + result.StandardError);
            var next = AiTestStaging.StageWithSharedDependencies(week, Path.Combine(fixture, "staging"));
            Assert.AreNotEqual(staged, next);
        }
        finally { Directory.Delete(fixture, true); }
    }

    [TestMethod]
    public void Stage_CopiesSolutionContents()
    {
        var source = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", "src-" + Guid.NewGuid().ToString("N"));
        var dest = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", "dest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(source, "Debug"));
        File.WriteAllText(Path.Combine(source, "Game.sln"), "sln");
        File.WriteAllText(Path.Combine(source, "Debug", "helper.dll"), "dll");
        Directory.CreateDirectory(Path.Combine(source, ".vs"));
        File.WriteAllText(Path.Combine(source, ".vs", "skip.txt"), "no");

        var staged = AiTestStaging.Stage(source, dest);
        var expected = Path.Combine(Path.GetFullPath(dest), Path.GetFileName(source));

        Assert.AreEqual(expected, staged);
        Assert.IsTrue(File.Exists(Path.Combine(staged, "Game.sln")));
        Assert.IsTrue(File.Exists(Path.Combine(staged, "Debug", "helper.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(staged, ".vs", "skip.txt")));
    }

    [TestMethod]
    public void Stage_DeletesExistingDestinationThenRecopies()
    {
        var source = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", "src-" + Guid.NewGuid().ToString("N"));
        var destRoot = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", "dest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Game.sln"), "new-sln");
        File.WriteAllText(Path.Combine(source, "Game.cpp"), "int main(){}");

        var leftover = Path.Combine(destRoot, Path.GetFileName(source));
        Directory.CreateDirectory(Path.Combine(leftover, "obj"));
        File.WriteAllText(Path.Combine(leftover, "Game.sln"), "old-sln");
        File.WriteAllText(Path.Combine(leftover, "stale.exe"), "old");
        File.WriteAllText(Path.Combine(leftover, "obj", "Game.obj"), "obj");

        var staged = AiTestStaging.Stage(source, destRoot);

        Assert.AreEqual(leftover, staged);
        Assert.AreEqual("new-sln", File.ReadAllText(Path.Combine(staged, "Game.sln")));
        Assert.IsTrue(File.Exists(Path.Combine(staged, "Game.cpp")));
        Assert.IsFalse(File.Exists(Path.Combine(staged, "stale.exe")));
        Assert.IsFalse(Directory.Exists(Path.Combine(staged, "obj")));
    }

    [TestMethod]
    public void Stage_SkipsIncrementalArtifactsAndRewritesOriginalPaths()
    {
        var source = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", "src-" + Guid.NewGuid().ToString("N"));
        var destRoot = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", "dest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(source, "obj"));
        Directory.CreateDirectory(Path.Combine(source, "bin", "Debug"));
        File.WriteAllText(Path.Combine(source, "Game.vcxproj"),
            $"<Project><AdditionalLibraryDirectories>{source}\\libs</AdditionalLibraryDirectories></Project>");
        File.WriteAllText(Path.Combine(source, "obj", "Game.obj"), "obj");
        File.WriteAllText(Path.Combine(source, "bin", "Debug", "Game.exe"), "exe");

        var staged = AiTestStaging.Stage(source, destRoot);
        var project = File.ReadAllText(Path.Combine(staged, "Game.vcxproj"));

        Assert.IsTrue(project.Contains(staged + "\\libs"));
        Assert.IsFalse(project.Contains(source + "\\libs"));
        Assert.IsFalse(Directory.Exists(Path.Combine(staged, "obj")));
        Assert.IsFalse(Directory.Exists(Path.Combine(staged, "bin")));
    }

    [TestMethod]
    public void RemapPath_KeepsRelativeLayout()
    {
        var mapped = AiTestStaging.RemapPath(
            @"C:\labs\Student\Debug\Game.exe",
            @"C:\labs\Student",
            @"C:\aitest");

        Assert.AreEqual(@"C:\aitest\Debug\Game.exe", mapped);
    }
}
