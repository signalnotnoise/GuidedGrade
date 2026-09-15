using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class RunnerWorkerTests
{
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(120000)]
    public async Task Worker_BuildsAndDrivesConsoleWithoutLlmCredentials(bool floodOutput)
    {
        var root = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Process? worker = null;
        try
        {
            // This fixture needs only installed framework packs, not user package sources or credentials.
            File.WriteAllText(Path.Combine(root, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
            File.WriteAllText(Path.Combine(root, "Hello.slnx"), "<Solution><Project Path=\"Hello.csproj\" /></Solution>");
            File.WriteAllText(Path.Combine(root, "Hello.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(root, "Program.cs"), (floodOutput ? "System.Console.Write(new string('x', 200_000));\n" : "") + """
                System.Console.WriteLine("Enter your name:");
                var input = System.Console.ReadLine();
                System.Console.WriteLine("Hello " + input);
                """);
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo != null && !File.Exists(Path.Combine(repo.FullName, "Lab Feedback WPF.slnx"))) repo = repo.Parent;
            Assert.IsNotNull(repo);
            var config = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
            var exe = Path.Combine(repo.FullName, "Lab Feedback Runner", "bin", config, "net10.0-windows", "LabFeedbackRunner.exe");
            Assert.IsTrue(File.Exists(exe), "Build the runner project before running worker integration tests.");
            var start = new ProcessStartInfo(exe)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(root);
            start.Environment["APPDATA"] = Path.Combine(root, "test-profile");
            worker = Process.Start(start)!;
            var errors = worker.StandardError.ReadToEndAsync();
            var build = await Request(worker, new { command = "build", entry = "Hello.slnx" });
            Assert.IsTrue(build.GetProperty("Succeeded").GetBoolean(), build.GetProperty("Log").GetString());
            var started = await Request(worker, new { command = "start" });
            Assert.IsTrue(started.GetProperty("started").GetBoolean(), started.GetProperty("error").GetString());
            var promptStatus = await WaitForOutput(worker, "Enter your name:");
            if (floodOutput)
            {
                var retained = promptStatus.GetProperty("standardOutput").GetString()!;
                Assert.IsTrue(retained.Length <= 64_000 + Lab_Feedback_WPF.Services.OutputLimits.TruncationMarker.Length);
                StringAssert.Contains(retained, Lab_Feedback_WPF.Services.OutputLimits.TruncationMarker);
            }
            await Request(worker, new { command = "input", text = "Ada" });
            var status = await WaitForOutput(worker, "Hello Ada");
            Assert.IsTrue(status.GetProperty("standardOutput").GetString()!.Contains("Hello Ada"));
            worker.StandardInput.Close();
            await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(0, worker.ExitCode, await errors);
        }
        finally
        {
            if (worker != null)
            {
                if (!worker.HasExited) worker.Kill(entireProcessTree: true);
                worker.Dispose();
            }
            Directory.Delete(root, true);
        }
    }

    private static async Task<JsonElement> WaitForOutput(Process worker, string expected)
    {
        string lastStatus = "";
        for (var attempt = 0; attempt < 15; attempt++)
        {
            var result = await Request(worker, new { command = "poll", idleMs = 200, windowMs = 1000 });
            var status = result.GetProperty("status");
            lastStatus = status.GetRawText();
            if (status.GetProperty("standardOutput").GetString()!.Contains(expected)) return status.Clone();
        }
        throw new AssertFailedException("Did not receive guest console output: " + expected + ". Status: " + lastStatus);
    }

    private static async Task<JsonElement> Request(Process worker, object request)
    {
        await worker.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request));
        await worker.StandardInput.FlushAsync();
        var line = await worker.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(100));
        Assert.IsNotNull(line);
        using var response = JsonDocument.Parse(line);
        Assert.IsTrue(response.RootElement.GetProperty("ok").GetBoolean(), line);
        return response.RootElement.GetProperty("result").Clone();
    }
}
