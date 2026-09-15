using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class OutputMemoryTests
{
    [TestMethod]
    public void TailBufferWrapsAndUsesAbsoluteCursors()
    {
        var buffer = new BoundedTextBuffer(8);
        buffer.Append("abcdef");
        var cursor = buffer.Written;
        buffer.Append("ghijk");
        Assert.AreEqual(OutputLimits.TruncationMarker + "defghijk", buffer.ToString());
        Assert.AreEqual("ghijk", buffer.ReadFrom(cursor));
        cursor = buffer.Written;
        buffer.Append("0123456789");
        Assert.AreEqual(OutputLimits.TruncationMarker + "23456789", buffer.ReadFrom(cursor));
        Assert.AreEqual(21L, buffer.Written);
        Assert.AreEqual(8, buffer.Length);
        Assert.AreEqual(string.Empty, buffer.ReadFrom(buffer.Written));
        buffer.Append("AB");
        Assert.AreEqual(OutputLimits.TruncationMarker + "456789AB", buffer.ToString());
    }

    [TestMethod]
    public void ProgressMailboxBoundsTinyAndOversizedChunksAndIgnoresDisposedProducers()
    {
        using var mailbox = new BoundedConsoleProgress();
        for (var i = 0; i < 100_000; i++) mailbox.Report(new ConsoleProgress { Text = "x" });
        var batch = mailbox.Drain();
        Assert.AreEqual(OutputLimits.PendingTerminalChunks + 1, batch.Count);
        Assert.IsTrue(batch[0].IsDiagnostic);
        Assert.AreEqual(0, mailbox.Drain().Count);
        mailbox.Report(new ConsoleProgress { Text = new string('y', 100_000) + "END", IsStderr = true });
        batch = mailbox.Drain();
        Assert.AreEqual(OutputLimits.PendingTerminalCharacters, batch[1].Text.Length);
        Assert.IsTrue(batch[1].IsStderr);
        StringAssert.EndsWith(batch[1].Text, "END");
        mailbox.Dispose();
        mailbox.Report(new ConsoleProgress { Text = "late output" });
        Assert.AreEqual(0, mailbox.Drain().Count);
    }

    [TestMethod]
    public async Task BuildCaptureDrainsLargeStreamWithoutAllocatingItsFullContents()
    {
        using var reader = new GeneratedReader(20_000_000);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var output = await ProcessRunner.CaptureOutputAsync(reader, CancellationToken.None);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(20_000_000, reader.ReadCharacters);
        Assert.AreEqual(new string('x', OutputLimits.BuildCharacters) + OutputLimits.TruncationMarker, output);
        Assert.IsTrue(allocated < 2_000_000, $"Allocated {allocated:N0} bytes for a 20-million-character stream.");
    }

    [TestMethod]
    public async Task BuildCaptureDoesNotMarkExactLimitAsTruncated()
    {
        using var reader = new GeneratedReader(OutputLimits.BuildCharacters);
        var output = await ProcessRunner.CaptureOutputAsync(reader, CancellationToken.None);
        Assert.AreEqual(new string('x', OutputLimits.BuildCharacters), output);
    }

    [TestMethod]
    public async Task BuildProcessDrainsBothFloodedPipesAndExits()
    {
        var result = await ProcessRunner.RunAsync("powershell.exe",
            "-NoProfile -Command \"$s = 'x' * 4096; for ($i=0; $i -lt 100; $i++) { [Console]::Out.Write($s); [Console]::Error.Write($s) }; exit 7\"",
            null, null, TimeSpan.FromSeconds(30));
        Assert.IsTrue(result.Started, result.Error);
        Assert.IsFalse(result.TimedOut, result.Error);
        Assert.AreEqual(7, result.ExitCode);
        Assert.AreEqual(OutputLimits.BuildCharacters + OutputLimits.TruncationMarker.Length, result.StandardOutput.Length);
        Assert.AreEqual(OutputLimits.BuildCharacters + OutputLimits.TruncationMarker.Length, result.StandardError.Length);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ConsoleFloodRetainsTailAndCanStillReadTheNextPrompt(bool useConPty)
    {
        var script = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ps1");
        await File.WriteAllTextAsync(script, """
            $s = 'x' * 4096
            for ($i=0; $i -lt 50; $i++) { [Console]::Out.Write($s) }
            [Console]::Out.WriteLine('READY_AFTER_FLOOD')
            [Console]::Out.Flush()
            $answer = [Console]::In.ReadLine()
            [Console]::Out.WriteLine('ANSWER=' + $answer)
            [Console]::Out.Flush()
            """);
        try
        {
            await using var session = InteractiveProcessSession.Start("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"", null, useConPty: useConPty);
            Assert.IsTrue(session.Started, session.Error);
            Assert.AreEqual(useConPty, session.UsesConPty);
            await WaitForText(session, "READY_AFTER_FLOOD");
            Assert.IsTrue(session.StandardOutput.Length <= OutputLimits.ConsoleCharacters + OutputLimits.TruncationMarker.Length);
            StringAssert.Contains(session.StandardOutput, OutputLimits.TruncationMarker);
            await session.WriteInputAsync("next");
            var slice = await WaitForText(session, "ANSWER=next");
            StringAssert.Contains(slice.StandardOutput, "ANSWER=next");
        }
        finally { File.Delete(script); }
    }

    private static async Task<ConsoleSlice> WaitForText(InteractiveProcessSession session, string text)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var slice = await session.WaitForIdleAsync(TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(1));
            if (slice.StandardOutput.Contains(text)) return slice;
            if (session.HasExited) break;
        }
        Assert.Fail($"Missing '{text}'. Retained {session.StandardOutput.Length} characters, exit {session.ExitCode}.");
        return new ConsoleSlice();
    }

    [TestMethod]
    public async Task FullBufferStillDetectsContinuousOutput()
    {
        await using var session = InteractiveProcessSession.Start("powershell.exe",
            "-NoProfile -Command \"$s = 'x' * 4096; while ($true) { [Console]::Out.Write($s); [Console]::Out.Flush(); Start-Sleep -Milliseconds 5 }\"", null);
        Assert.IsTrue(session.Started, session.Error);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (!session.StandardOutput.Contains(OutputLimits.TruncationMarker) && DateTime.UtcNow < deadline)
            await session.WaitForIdleAsync(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(400));
        StringAssert.Contains(session.StandardOutput, OutputLimits.TruncationMarker);
        var slice = await session.WaitForIdleAsync(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(600));
        Assert.IsTrue(slice.OutputStillFlowing, "A full tail buffer must not look idle while output continues.");
        Assert.IsFalse(session.HasExited);
        Assert.IsTrue(slice.StandardOutput.Length <= OutputLimits.ConsoleCharacters + OutputLimits.TruncationMarker.Length);
    }

    private sealed class GeneratedReader(int total) : TextReader
    {
        public int ReadCharacters { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(buffer.Length, total - ReadCharacters);
            buffer.Span[..count].Fill('x');
            ReadCharacters += count;
            return ValueTask.FromResult(count);
        }
    }
}
