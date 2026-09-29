using System.Diagnostics;
using System.IO;
using System.Text;

namespace GuidedGrade.Services
{
    public sealed class ProcessRunResult
    {
        public bool Started { get; init; }
        public bool TimedOut { get; init; }
        public int? ExitCode { get; init; }
        public string StandardOutput { get; init; } = string.Empty;
        public string StandardError { get; init; } = string.Empty;
        public string? Error { get; init; }
    }

    public static class ProcessRunner
    {
        public static async Task<ProcessRunResult> RunAsync(
            string fileName,
            string arguments,
            string? workingDirectory,
            string? stdin,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Environment.CurrentDirectory
                    : workingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false)
            };

            Process? process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                return new ProcessRunResult
                {
                    Started = false,
                    Error = ex.Message
                };
            }

            if (process == null)
            {
                return new ProcessRunResult
                {
                    Started = false,
                    Error = $"Failed to start '{fileName}'."
                };
            }

            using (process)
            {
                var stdoutTask = CaptureOutputAsync(process.StandardOutput, cancellationToken);
                var stderrTask = CaptureOutputAsync(process.StandardError, cancellationToken);

                try
                {
                    if (!string.IsNullOrEmpty(stdin))
                    {
                        await process.StandardInput.WriteAsync(stdin.AsMemory(), cancellationToken).ConfigureAwait(false);
                        await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }

                    process.StandardInput.Close();
                }
                catch (Exception ex)
                {
                    TryKill(process);
                    await SafeRead(stdoutTask);
                    await SafeRead(stderrTask);
                    cancellationToken.ThrowIfCancellationRequested();
                    return new ProcessRunResult
                    {
                        Started = true,
                        Error = $"Failed to write stdin: {ex.Message}"
                    };
                }

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(timeout);

                try
                {
                    await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                    var stdout = await stdoutTask.ConfigureAwait(false);
                    var stderr = await stderrTask.ConfigureAwait(false);

                    return new ProcessRunResult
                    {
                        Started = true,
                        ExitCode = process.ExitCode,
                        StandardOutput = stdout,
                        StandardError = stderr
                    };
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    TryKill(process);
                    return new ProcessRunResult
                    {
                        Started = true,
                        TimedOut = true,
                        StandardOutput = await SafeRead(stdoutTask),
                        StandardError = await SafeRead(stderrTask),
                        Error = $"Timed out after {timeout.TotalSeconds:0}s."
                    };
                }
                finally
                {
                    TryKill(process);
                    await SafeRead(stdoutTask);
                    await SafeRead(stderrTask);
                }
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        }

        private static async Task<string> SafeRead(Task<string> task)
        {
            try
            {
                return await task.WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static async Task<string> CaptureOutputAsync(TextReader reader, CancellationToken cancellationToken)
        {
            var kept = new StringBuilder(OutputLimits.BuildCharacters);
            var buffer = new char[4096];
            var truncated = false;
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                var take = Math.Min(read, OutputLimits.BuildCharacters - kept.Length);
                kept.Append(buffer, 0, take);
                truncated |= take < read;
                // Continue draining both streams after the cap so the child cannot block on full pipes.
            }
            if (truncated) kept.Append(OutputLimits.TruncationMarker);
            return kept.ToString();
        }
    }
}
