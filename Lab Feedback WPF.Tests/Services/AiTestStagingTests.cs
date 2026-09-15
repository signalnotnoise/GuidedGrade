using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class AiTestStagingTests
{
    [TestMethod]
    public void Stage_CopiesSolutionContents()
    {
        var source = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", "src-" + Guid.NewGuid().ToString("N"));
        var dest = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", "dest-" + Guid.NewGuid().ToString("N"));
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
        var source = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", "src-" + Guid.NewGuid().ToString("N"));
        var destRoot = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", "dest-" + Guid.NewGuid().ToString("N"));
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
        var source = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", "src-" + Guid.NewGuid().ToString("N"));
        var destRoot = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", "dest-" + Guid.NewGuid().ToString("N"));
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
