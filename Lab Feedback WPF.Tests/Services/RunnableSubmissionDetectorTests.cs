using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class RunnableSubmissionDetectorTests
{
    [TestMethod]
    public void IsSolutionFile_RecognizesSlnAndSlnx()
    {
        Assert.IsTrue(RunnableSubmissionDetector.IsSolutionFile(@"C:\labs\StudentLab.sln"));
        Assert.IsTrue(RunnableSubmissionDetector.IsSolutionFile(@"C:\labs\StudentLab.slnx"));
        Assert.IsFalse(RunnableSubmissionDetector.IsSolutionFile(@"C:\labs\main.cpp"));
    }

    [TestMethod]
    public void Detect_FindsSolutionFile()
    {
        var root = CreateTempDir();
        var sln = Path.Combine(root, "StudentLab.sln");
        File.WriteAllText(sln, "Microsoft Visual Studio Solution File");
        File.WriteAllText(Path.Combine(root, "main.cpp"), "int main() { return 0; }");

        var result = RunnableSubmissionDetector.Detect(Path.Combine(root, "main.cpp"), root);

        Assert.IsNotNull(result);
        Assert.AreEqual(SubmissionKind.VisualStudioSolution, result!.Kind);
        Assert.AreEqual(sln, result.EntryPath);
    }

    [TestMethod]
    public void Detect_UsesPrimarySolutionPath()
    {
        var root = CreateTempDir();
        var sln = Path.Combine(root, "App.sln");
        File.WriteAllText(sln, "Microsoft Visual Studio Solution File");

        var result = RunnableSubmissionDetector.Detect(sln, root);

        Assert.IsNotNull(result);
        Assert.AreEqual(sln, result!.EntryPath);
        Assert.AreEqual(SubmissionKind.VisualStudioSolution, result.Kind);
    }

    [TestMethod]
    public void Detect_ReturnsNullWithoutSolution()
    {
        var root = CreateTempDir();
        var cpp = Path.Combine(root, "main.cpp");
        File.WriteAllText(cpp, "int main() { return 0; }");

        var result = RunnableSubmissionDetector.Detect(cpp, root);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void FindRunnableOutput_PrefersAppExeOverTestHost()
    {
        var root = CreateTempDir();
        var appDir = Path.Combine(root, "bin", "Debug");
        var testDir = Path.Combine(root, "tests", "bin", "Debug");
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(testDir);

        var appExe = Path.Combine(appDir, "StudentLab.exe");
        var testHost = Path.Combine(testDir, "testhost.exe");
        File.WriteAllText(appExe, "app");
        File.WriteAllText(testHost, "host");
        File.WriteAllText(Path.Combine(appDir, "StudentLab.runtimeconfig.json"), "{}");
        File.SetLastWriteTimeUtc(testHost, DateTime.UtcNow.AddMinutes(5));

        var result = RunnableSubmissionDetector.FindRunnableOutput(root, "StudentLab");

        Assert.AreEqual(appExe, result);
    }

    [TestMethod]
    public void FindPrimaryVcxproj_PrefersNestedProjectFolder()
    {
        var root = CreateTempDir();
        var projectDir = Path.Combine(root, "RPG_Shop");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(root, "Final_Practical_RPG_Shop.sln"), "sln");
        var vcxproj = Path.Combine(projectDir, "RPG_Shop.vcxproj");
        File.WriteAllText(vcxproj, "<Project/>");

        var result = RunnableSubmissionDetector.FindPrimaryVcxproj(root, "Final_Practical_RPG_Shop");

        Assert.AreEqual(vcxproj, result);
        Assert.AreEqual("x64", RunnableSubmissionDetector.InferNativePlatform(projectDir));
        StringAssert.EndsWith(
            RunnableSubmissionDetector.GetNativeOutDir(projectDir, "x64").TrimEnd('\\', '/'),
            Path.Combine("RPG_Shop", "x64", "Debug"));
    }

    [TestMethod]
    public void FindRunnableOutput_PrefersProjectX64DebugWithDlls()
    {
        var root = CreateTempDir();
        var solutionOut = Path.Combine(root, "X64", "Debug");
        var projectOut = Path.Combine(root, "RPG_Shop", "x64", "Debug");
        Directory.CreateDirectory(solutionOut);
        Directory.CreateDirectory(projectOut);

        var wrongExe = Path.Combine(solutionOut, "RPG_Shop.exe");
        var rightExe = Path.Combine(projectOut, "RPG_Shop.exe");
        File.WriteAllText(wrongExe, "wrong");
        File.WriteAllText(rightExe, "right");
        File.WriteAllText(Path.Combine(projectOut, "sfml.dll"), "dll");
        File.SetLastWriteTimeUtc(wrongExe, DateTime.UtcNow.AddMinutes(5));

        var result = RunnableSubmissionDetector.FindRunnableOutput(root, "RPG_Shop");

        Assert.AreEqual(rightExe, result);
    }

    [TestMethod]
    public void FindRunnableOutput_PrefersDebugExeOverSolutionRoot()
    {
        var root = CreateTempDir();
        var debugDir = Path.Combine(root, "RPG_Shop", "Debug");
        Directory.CreateDirectory(debugDir);

        var nestedExe = Path.Combine(debugDir, "RPG_Shop.exe");
        var rootExe = Path.Combine(root, "RPG_Shop.exe");
        File.WriteAllText(nestedExe, "nested");
        File.WriteAllText(rootExe, "root");
        File.SetLastWriteTimeUtc(rootExe, DateTime.UtcNow.AddMinutes(5));

        var result = RunnableSubmissionDetector.FindRunnableOutput(root, "RPG_Shop");

        Assert.AreEqual(nestedExe, result);
    }

    [TestMethod]
    public void FindNewestExecutable_IgnoresTestHost()
    {
        var root = CreateTempDir();
        Directory.CreateDirectory(root);
        var exe = Path.Combine(root, "lab.exe");
        File.WriteAllText(exe, "app");
        File.WriteAllText(Path.Combine(root, "testhost.exe"), "host");

        var result = RunnableSubmissionDetector.FindNewestExecutable(root);

        Assert.AreEqual(exe, result);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
