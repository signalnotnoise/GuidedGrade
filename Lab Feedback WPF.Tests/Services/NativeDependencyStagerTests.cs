using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class NativeDependencyStagerTests
{
    [TestMethod]
    public void CopyDependencies_PreservesBuiltAssemblyAndSkipsReferenceAssemblies()
    {
        var root = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "bin");
        var references = Path.Combine(root, "obj", "refint");
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(references);
        try
        {
            var built = Path.Combine(output, "Game.dll");
            File.WriteAllText(built, "correct build");
            File.WriteAllText(Path.Combine(root, "Game.dll"), "stale build");
            File.WriteAllText(Path.Combine(references, "ReferenceOnly.dll"), "reference only");
            NativeDependencyStager.CopyDependencies(Path.Combine(output, "Game.exe"), root);
            Assert.AreEqual("correct build", File.ReadAllText(built));
            Assert.IsFalse(File.Exists(Path.Combine(output, "ReferenceOnly.dll")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void CopyDependencies_CopiesDllsNextToExe()
    {
        var root = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", Guid.NewGuid().ToString("N"));
        var solutionOut = Path.Combine(root, "X64", "Debug");
        var projectOut = Path.Combine(root, "RPG_Shop", "x64", "Debug");
        Directory.CreateDirectory(solutionOut);
        Directory.CreateDirectory(projectOut);

        var exe = Path.Combine(projectOut, "RPG_Shop.exe");
        File.WriteAllText(exe, "exe");
        File.WriteAllText(Path.Combine(solutionOut, "sfml-graphics-d-2.dll"), "dll");

        var copied = NativeDependencyStager.CopyDependencies(exe, root);

        Assert.IsTrue(copied >= 1);
        Assert.IsTrue(File.Exists(Path.Combine(projectOut, "sfml-graphics-d-2.dll")));
    }
}
