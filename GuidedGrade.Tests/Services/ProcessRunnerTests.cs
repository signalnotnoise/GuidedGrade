using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class ProcessRunnerTests
{
    [TestMethod]
    public async Task RunAsync_CapturesStdout()
    {
        var result = await ProcessRunner.RunAsync(
            "cmd.exe",
            "/c echo hello-lab",
            null,
            null,
            TimeSpan.FromSeconds(10));

        Assert.IsTrue(result.Started);
        Assert.IsFalse(result.TimedOut);
        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.StandardOutput, "hello-lab");
    }

    [TestMethod]
    public async Task RunAsync_PipesStdin()
    {
        var result = await ProcessRunner.RunAsync(
            "powershell.exe",
            "-NoProfile -Command \"[Console]::In.ReadToEnd()\"",
            null,
            "break-me" + Environment.NewLine,
            TimeSpan.FromSeconds(15));

        Assert.IsTrue(result.Started);
        Assert.IsFalse(result.TimedOut);
        StringAssert.Contains(result.StandardOutput, "break-me");
    }

    [TestMethod]
    public async Task RunAsync_TimesOutHungProcess()
    {
        var result = await ProcessRunner.RunAsync(
            "cmd.exe",
            "/c ping -n 30 127.0.0.1 >nul",
            null,
            null,
            TimeSpan.FromMilliseconds(500));

        Assert.IsTrue(result.Started);
        Assert.IsTrue(result.TimedOut);
    }
}
