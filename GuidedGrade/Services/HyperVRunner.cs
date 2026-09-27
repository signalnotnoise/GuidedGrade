using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using GuidedGrade.Models;

namespace GuidedGrade.Services;

internal sealed class HyperVRunner : IInteractiveConsoleSession
{
    private readonly Process _bridge;
    private readonly string _runDirectory;
    private readonly IProgress<ConsoleProgress>? _progress;
    private readonly SemaphoreSlim _requests = new(1);
    private readonly Task<string> _stderr;
    private bool _disposed;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public bool Started { get; private set; }
    public string? Error { get; private set; }
    public bool HasExited { get; private set; } = true;
    public int? ExitCode { get; private set; }
    public string StandardOutput { get; private set; } = "";
    public string StandardError { get; private set; } = "";

    private HyperVRunner(Process bridge, string runDirectory, IProgress<ConsoleProgress>? progress)
    {
        _bridge = bridge;
        _runDirectory = runDirectory;
        _progress = progress;
        _stderr = DrainErrorsAsync(bridge.StandardError);
    }

    internal static string? ConfigurationError(LLMSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.RunnerBaseDisk) || !File.Exists(settings.RunnerBaseDisk))
            return "Select a prepared, detached Windows VHDX template in LLM Settings > Execution.";
        if (!settings.RunnerBaseDisk.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase))
            return "The runner template must be a VHDX disk.";
        if (!File.Exists(settings.RunnerCredentialFile))
            return "Select the guest credential XML file in LLM Settings > Execution. See EXECUTION_SAFETY.md for setup.";
        if (!File.Exists(Path.Combine(settings.RunnerWorkerFolder ?? "", "GuidedGrade.Runner.exe")))
            return "Publish GuidedGrade.Runner and select its publish folder in LLM Settings > Execution.";
        if (settings.RunnerMemoryMb is < 2048 or > 16384)
            return "Runner memory must be between 2048 and 16384 MB.";
        return null;
    }

    public static async Task<HyperVRunner> CreateAsync(LLMSettings settings, string submissionRoot,
        IProgress<ConsoleProgress>? progress, CancellationToken cancellationToken)
    {
        var error = ConfigurationError(settings);
        if (error != null) throw new InvalidOperationException(error);
        ValidateInputSeparation(settings, submissionRoot);
        var script = Path.Combine(AppContext.BaseDirectory, "Runner", "HyperVBridge.ps1");
        if (!File.Exists(script)) throw new FileNotFoundException("The Hyper-V bridge script is missing.", script);
        var runDirectory = Path.Combine(AppDataPaths.LocalDirectory, "Runners", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDirectory);
        HyperVRunner? runner = null;
        try
        {
            progress?.Report(new ConsoleProgress { Text = "Preparing an isolated Windows VM (no network adapter)...\n" });
            await Task.Run(() =>
            {
                PackageDirectory(submissionRoot, Path.Combine(runDirectory, "submission.zip"), cancellationToken);
                PackageDirectory(settings.RunnerWorkerFolder, Path.Combine(runDirectory, "worker.zip"), cancellationToken);
            }, cancellationToken);
            var start = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false)
            };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script })
                start.ArgumentList.Add(argument);
            runner = new HyperVRunner(Process.Start(start) ?? throw new IOException("Could not start the Hyper-V bridge."), runDirectory, progress);
            await runner.RequestAsync(new
            {
                baseDisk = Path.GetFullPath(settings.RunnerBaseDisk),
                credentialFile = Path.GetFullPath(settings.RunnerCredentialFile),
                runDirectory, memoryMb = settings.RunnerMemoryMb,
                submissionArchive = Path.Combine(runDirectory, "submission.zip"),
                workerArchive = Path.Combine(runDirectory, "worker.zip")
            }, cancellationToken, TimeSpan.FromMinutes(5));
            return runner;
        }
        catch
        {
            if (runner != null) await runner.DisposeAsync();
            else DeleteOwnedDirectory(runDirectory);
            throw;
        }
    }

    internal static void PackageDirectory(string source, string destination, CancellationToken token)
    {
        source = Path.GetFullPath(source);
        using var zip = ZipFile.Open(destination, ZipArchiveMode.Create);
        var pending = new Stack<string>();
        pending.Push(source);
        long total = 0;
        int count = 0;
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Runner inputs cannot contain symbolic links or junctions.");
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                token.ThrowIfCancellationRequested();
                var info = new FileInfo(file);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Runner inputs cannot contain symbolic links.");
                total += info.Length;
                if (++count > 20000 || total > 1024L * 1024 * 1024)
                    throw new IOException("Runner input exceeds 20,000 files or 1 GB.");
                zip.CreateEntryFromFile(file, Path.GetRelativePath(source, file), CompressionLevel.Fastest);
            }
            foreach (var child in Directory.EnumerateDirectories(directory))
                if (Path.GetFileName(child) is not (".git" or ".vs")) pending.Push(child);
        }
    }

    internal static void ValidateInputSeparation(LLMSettings settings, string submissionRoot)
    {
        foreach (var inputRoot in new[] { submissionRoot, settings.RunnerWorkerFolder })
        {
            var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(inputRoot)) + Path.DirectorySeparatorChar;
            foreach (var protectedFile in new[] { settings.RunnerCredentialFile, settings.RunnerBaseDisk })
                if (Path.GetFullPath(protectedFile).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Keep the guest credential file and template disk outside submission and worker folders.");
        }
    }

    public async Task<SubmissionBuilder.BuildResult> BuildAsync(string relativeEntry, CancellationToken token)
        => (await RequestAsync(new { command = "build", entry = relativeEntry }, token))
            .Deserialize<SubmissionBuilder.BuildResult>(JsonOptions) ?? throw new IOException("Invalid build response.");

    public async Task<SubmissionBuilder.BuildResult> ResolveExistingAsync(string relativeEntry, CancellationToken token)
        => (await RequestAsync(new { command = "resolve", entry = relativeEntry }, token))
            .Deserialize<SubmissionBuilder.BuildResult>(JsonOptions) ?? throw new IOException("Invalid resolve response.");

    public async Task StartAsync(CancellationToken token) => UpdateStatus(await RequestAsync(new { command = "start" }, token));

    public async Task<ConsoleSlice> WaitForIdleAsync(TimeSpan idle, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var result = await RequestAsync(new { command = "poll", idleMs = (int)idle.TotalMilliseconds, windowMs = (int)window.TotalMilliseconds }, cancellationToken);
        UpdateStatus(result.GetProperty("status"));
        var slice = result.GetProperty("slice").Deserialize<ConsoleSlice>(JsonOptions)!;
        if (!string.IsNullOrEmpty(slice.StandardOutput)) _progress?.Report(new ConsoleProgress { Text = slice.StandardOutput });
        if (!string.IsNullOrEmpty(slice.StandardError)) _progress?.Report(new ConsoleProgress { Text = slice.StandardError, IsStderr = true });
        return slice;
    }

    public async Task WriteInputAsync(string text, CancellationToken cancellationToken = default)
    {
        UpdateStatus(await RequestAsync(new { command = "input", text }, cancellationToken));
        _progress?.Report(new ConsoleProgress { Text = text + "\n", IsInput = true });
    }
    public void CloseInput() => UpdateStatus(RequestAsync(new { command = "close" }, CancellationToken.None).GetAwaiter().GetResult());
    public void Kill()
    {
        try { UpdateStatus(RequestAsync(new { command = "kill" }, CancellationToken.None, TimeSpan.FromSeconds(10)).GetAwaiter().GetResult()); }
        catch { /* VM destruction in DisposeAsync is the final cleanup boundary. */ }
    }

    private void UpdateStatus(JsonElement status)
    {
        Started = status.GetProperty("started").GetBoolean();
        Error = status.GetProperty("error").GetString();
        HasExited = status.GetProperty("hasExited").GetBoolean();
        ExitCode = status.GetProperty("exitCode").ValueKind == JsonValueKind.Number ? status.GetProperty("exitCode").GetInt32() : null;
        StandardOutput = status.GetProperty("standardOutput").GetString() ?? "";
        StandardError = status.GetProperty("standardError").GetString() ?? "";
    }

    private async Task<JsonElement> RequestAsync(object request, CancellationToken token, TimeSpan? timeout = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(130));
        await _requests.WaitAsync(deadline.Token).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(request);
            if (json.Length > 65536) throw new IOException("Runner request is too large.");
            await _bridge.StandardInput.WriteLineAsync(json.AsMemory(), deadline.Token).ConfigureAwait(false);
            await _bridge.StandardInput.FlushAsync(deadline.Token).ConfigureAwait(false);
            var line = await ReadBoundedLineAsync(_bridge.StandardOutput, deadline.Token).ConfigureAwait(false);
            if (line == null)
            {
                var details = await _stderr.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                throw new IOException("Hyper-V runner stopped. Check Hyper-V permissions and configuration. " + details);
            }
            using var result = JsonDocument.Parse(line);
            if (!result.RootElement.GetProperty("ok").GetBoolean())
                throw new IOException(result.RootElement.GetProperty("error").GetString());
            return result.RootElement.GetProperty("result").Clone();
        }
        finally { _requests.Release(); }
    }

    internal static async Task<string?> ReadBoundedLineAsync(TextReader reader, CancellationToken token)
    {
        var line = new StringBuilder();
        var character = new char[1];
        while (await reader.ReadAsync(character.AsMemory(), token).ConfigureAwait(false) != 0)
        {
            if (character[0] == '\n') return line.ToString().TrimEnd('\r');
            if (line.Length >= 1048576) throw new IOException("Guest response exceeds the protocol limit.");
            line.Append(character[0]);
        }
        return line.Length == 0 ? null : line.ToString();
    }

    private static async Task<string> DrainErrorsAsync(StreamReader reader)
    {
        var kept = new StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
            if (kept.Length < 8000) kept.Append(buffer, 0, Math.Min(read, 8000 - kept.Length));
        return kept.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (!_bridge.HasExited)
            {
                await _bridge.StandardInput.WriteLineAsync("{\"command\":\"shutdown\"}").ConfigureAwait(false);
                _bridge.StandardInput.Close();
                await _bridge.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            }
        }
        catch
        {
            try { if (!_bridge.HasExited) _bridge.Kill(); } catch { }
            _progress?.Report(new ConsoleProgress { Text = "Runner bridge interrupted; the independent VM watchdog will stop the guest.\n", IsStderr = true });
        }
        finally
        {
            _bridge.Dispose();
            try { DeleteOwnedDirectory(_runDirectory); }
            catch { _progress?.Report(new ConsoleProgress { Text = "Runner files retained for cleanup: " + _runDirectory + "\n", IsStderr = true }); }
        }
    }

    private static void DeleteOwnedDirectory(string path)
    {
        var root = Path.GetFullPath(Path.Combine(AppDataPaths.LocalDirectory, "Runners"));
        var resolved = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(resolved), root, StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParseExact(Path.GetFileName(resolved), "N", out _))
            throw new IOException("Refusing to remove a directory outside the owned runner workspace.");
        Directory.Delete(resolved, true);
    }
}
