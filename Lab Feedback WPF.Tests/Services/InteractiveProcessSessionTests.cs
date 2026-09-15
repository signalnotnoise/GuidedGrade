using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class InteractiveProcessSessionTests
{
    [TestMethod]
    public async Task WaitForIdleAsync_ReadsStdoutWithoutStdin()
    {
        await using var session = InteractiveProcessSession.Start(
            "cmd.exe",
            "/c echo hello-lab",
            null);

        Assert.IsTrue(session.Started);
        var slice = await session.WaitForIdleAsync(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(8));

        StringAssert.Contains(session.StandardOutput, "hello-lab");
        Assert.IsTrue(slice.Exited || session.HasExited);
    }

    [TestMethod]
    public async Task WaitForIdleAsync_ConPtyCapturesConsoleOutput()
    {
        await using var session = InteractiveProcessSession.Start(
            "cmd.exe",
            "/c echo hello-pty",
            null,
            useConPty: true);

        Assert.IsTrue(session.Started, session.Error);
        Assert.IsTrue(session.UsesConPty, session.Error);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            await session.WaitForIdleAsync(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1));
            if (session.StandardOutput.Contains("hello-pty"))
                return;
            if (session.HasExited)
                break;
        }

        StringAssert.Contains(session.StandardOutput, "hello-pty", $"Exit: {session.ExitCode}");
    }

    [TestMethod]
    public void HasVisibleConsoleText_IgnoresVtHandshake()
    {
        Assert.IsFalse(InteractiveProcessSession.HasVisibleConsoleText("\u001b[?9001h\u001b[?1004h"));
        Assert.IsTrue(InteractiveProcessSession.HasVisibleConsoleText("\u001b[?9001hhello-pty"));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WriteInputAsync_SendsLinesAfterReading(bool useConPty)
    {
        var script = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", Guid.NewGuid().ToString("N") + ".ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(script)!);
        await File.WriteAllTextAsync(script, """
            [Console]::Out.Write('P1')
            [Console]::Out.Flush()
            $a = [Console]::In.ReadLine()
            [Console]::Out.WriteLine('A=' + $a)
            [Console]::Out.Flush()
            [Console]::Out.Write('P2')
            [Console]::Out.Flush()
            $b = [Console]::In.ReadLine()
            [Console]::Out.WriteLine('B=' + $b)
            [Console]::Out.Flush()
            """);

        await using var session = InteractiveProcessSession.Start(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
            null, useConPty: useConPty);

        Assert.IsTrue(session.Started);
        Assert.AreEqual(useConPty, session.UsesConPty);
        await WaitForOutputAsync(session, "P1", TimeSpan.FromSeconds(15));

        await session.WriteInputAsync("one\n");
        await WaitForOutputAsync(session, "A=one", TimeSpan.FromSeconds(10));

        await session.WriteInputAsync("two\r\n");
        await WaitForOutputAsync(session, "B=two", TimeSpan.FromSeconds(10));
        Assert.AreEqual(0, session.ExitCode);
    }

    private static async Task WaitForOutputAsync(InteractiveProcessSession session, string expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await session.WaitForIdleAsync(TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(1));
            if (session.StandardOutput.Contains(expected) || session.StandardError.Contains(expected))
                return;
            if (session.HasExited)
                break;
        }

        Assert.Fail($"Did not see '{expected}'. STDOUT='{session.StandardOutput}' STDERR='{session.StandardError}'");
    }
}

[TestClass]
public class ConsoleDriverAgentTests
{
    [TestMethod]
    public async Task DriveCoreAsync_TurnLimitStopsProcessWithoutReportingStudentFailure()
    {
        await using var session = InteractiveProcessSession.Start(
            "cmd.exe", "/c echo Waiting for more interaction & ping -n 30 127.0.0.1 >nul", null);
        Assert.IsTrue(session.Started);

        var result = await ConsoleDriverAgent.DriveCoreAsync(session,
            (_, _, _) => Task.FromResult(new ConsoleAgentAction { Input = "1" }), maxTurns: 1);

        Assert.IsTrue(result.StoppedByRunner);
        Assert.IsFalse(result.InfiniteLoop);
        Assert.IsFalse(result.Crashed);
        Assert.IsNull(result.ExitCode, "Cleanup exit codes must not become grading evidence.");
        Assert.IsTrue(result.Findings.Any(f => f.StartsWith("INCONCLUSIVE:")));
        Assert.IsFalse(result.Findings.Any(f => f.StartsWith("CRASH:") || f.StartsWith("FAILURE:") || f.StartsWith("INFINITE LOOP:")));
    }

    [TestMethod]
    public void BuildFindings_TimeBudgetIsInconclusive()
    {
        var findings = ConsoleDriverAgent.BuildFindings(true, false, null, "Choose an option:", "", null);
        Assert.IsTrue(findings.Any(f => f.StartsWith("INCONCLUSIVE:")));
        Assert.IsFalse(findings.Any(f => f.StartsWith("HANG") || f.StartsWith("INFINITE LOOP") || f.StartsWith("FAILURE:")));
    }

    [TestMethod]
    public void BuildFindings_RunnerTerminationIsNotACrash()
    {
        var findings = ConsoleDriverAgent.BuildFindings(false, false,
            unchecked((int)0xC0000005), "Choose an option:", "", null, stoppedByRunner: true);
        Assert.IsFalse(findings.Any(f => f.StartsWith("CRASH:") || f.StartsWith("FAILURE:")));
        Assert.IsTrue(findings.Any(f => f.StartsWith("INCONCLUSIVE:")));
    }

    [TestMethod]
    public void ModelObservation_DoesNotBecomeVerifiedFailure()
    {
        var findings = new List<string>();
        ConsoleDriverAgent.RecordObservation(new ConsoleAgentAction
        {
            Observation = "no crash or infinite loop", Reason = "menu displayed again"
        }, findings);
        Assert.AreEqual(1, findings.Count);
        StringAssert.StartsWith(findings[0], "MODEL OBSERVATION (unverified");
    }

    [TestMethod]
    public void Parse_ReadsTypeAction()
    {
        var action = ConsoleDriverAgent.Parse("""{"action":"type","input":"5","reason":"asked for a number"}""", 1);
        Assert.AreEqual("type", action.Action);
        Assert.AreEqual("5", action.ResolvedInput);
    }

    [TestMethod]
    public void Parse_ReadsCloseFromMarkdown()
    {
        var action = ConsoleDriverAgent.Parse("""
            ```json
            {"action":"close","reason":"program finished"}
            ```
            """, 2);
        Assert.AreEqual("close", action.Action);
    }

    [TestMethod]
    public void BuildFindings_RecordsCrashAndHang()
    {
        var findings = ConsoleDriverAgent.BuildFindings(
            timedOut: true,
            infiniteLoop: true,
            exitCode: unchecked((int)0xC0000005),
            stdout: "",
            stderr: "access violation",
            extra: null);

        Assert.IsTrue(findings.Any(f => f.StartsWith("CRASH:")));
        Assert.IsTrue(findings.Any(f => f.Contains("INFINITE LOOP") || f.Contains("HANG")));
    }

    [TestMethod]
    public void Parse_RejectsPlainTextWithoutTypingIt()
    {
        var action = ConsoleDriverAgent.Parse("hello-input", 1);
        Assert.AreEqual("stop", action.Action);
        Assert.AreEqual("", action.ResolvedInput);
    }
}
