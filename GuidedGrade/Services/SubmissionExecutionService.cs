using System.Diagnostics;
using System.IO;
using System.Text;
using GuidedGrade.Models;

namespace GuidedGrade.Services
{
    public sealed class SubmissionExecutionService : SubmissionBuilder
    {
        private readonly LLMSettings _settings;
        private readonly Func<string, bool>? _confirmLocal;

        public SubmissionExecutionService(LLMSettings? settings = null, Func<string, bool>? confirmLocal = null)
        {
            _settings = settings ?? LLMSettings.Load();
            _confirmLocal = confirmLocal;
        }

        internal SubmissionExecutionService(RunProcess runProcess, Func<string, CancellationToken, Task<string?>>? toolResolver)
            : base(runProcess, toolResolver) { _settings = new LLMSettings(); }


        public async Task<string> ExecuteAndFormatAsync(
            string primaryFilePath,
            string? searchRoot,
            string requirements,
            IEnumerable<string>? identifiers,
            IReadOnlyList<RelatedSubmissionFile>? relatedFiles,
            LLMSettings settings,
            IProgress<ConsoleProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (settings.ExecutionMode == SubmissionExecutionMode.HyperV)
                return await ExecuteIsolatedAsync(primaryFilePath, searchRoot, requirements, identifiers, relatedFiles, settings, progress, cancellationToken);
            if (!SubmissionExecutionPolicy.IsLocalAuthorized(settings, _confirmLocal, primaryFilePath))
                return SubmissionExecutionPolicy.LocalDeclined;

            var submission = RunnableSubmissionDetector.Detect(primaryFilePath, searchRoot);
            if (submission == null)
            {
                return "# RUNTIME EXECUTION RESULTS\n" +
                       "The submission did not appear to be a standalone runnable program or project. " +
                       "Do not deduct points for inability to execute. Grade from source only.";
            }

            string stageLog;
            try
            {
                var original = submission;
                submission = await Task.Run(() => StageSubmission(original), cancellationToken)
                    .ConfigureAwait(false);
                stageLog = $"Staged copy: {submission.RootDirectory}";
            }
            catch (Exception ex)
            {
                return "# RUNTIME EXECUTION RESULTS\n" +
                       $"Could not copy the submission to {AiTestStaging.DefaultRoot}: {ex.Message}";
            }

            var build = await BuildAsync(submission, cancellationToken);
            var sb = new StringBuilder();
            sb.AppendLine("# RUNTIME EXECUTION RESULTS");
            sb.AppendLine($"Detected: {submission.Kind}");
            sb.AppendLine(stageLog);
            sb.AppendLine($"Build: {(build.Succeeded ? "succeeded" : "failed")}");
            if (!string.IsNullOrWhiteSpace(build.Log))
            {
                sb.AppendLine("Build log:");
                sb.AppendLine(Sanitize(build.Log, identifiers));
            }

            if (!build.Succeeded || string.IsNullOrWhiteSpace(build.CommandFileName))
            {
                sb.AppendLine("The program could not be executed. Check staging, dependencies, toolchain and file locks before attributing build failures to student source. Inability to execute alone is not grounds for a deduction.");
                return sb.ToString();
            }

            sb.AppendLine($"Program: {build.CommandFileName} {build.Arguments}".Trim());
            sb.AppendLine($"Working directory: {build.WorkingDirectory}");

            var copiedDlls = 0;
            if (build.CommandFileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                copiedDlls = NativeDependencyStager.CopyDependencies(build.CommandFileName, submission.RootDirectory);
                sb.AppendLine($"Copied {copiedDlls} DLL(s) next to the executable.");
            }

            var plannerSource = BuildPlannerSource(submission, relatedFiles, identifiers);
            var useConPty = InteractiveProcessSession.IsConsoleSubsystemExecutable(build.CommandFileName);

            await using var session = InteractiveProcessSession.Start(
                build.CommandFileName,
                build.Arguments,
                build.WorkingDirectory,
                NativeDependencyStager.GetDllDirectories(submission.RootDirectory, build.CommandFileName),
                progress,
                useConPty);

            progress?.Report(new ConsoleProgress
            {
                Text = $"Program: {build.CommandFileName} {build.Arguments}{Environment.NewLine}Working directory: {build.WorkingDirectory}{Environment.NewLine}Console capture: {(session.UsesConPty ? "ConPTY" : "redirected streams")}{Environment.NewLine}"
            });

            sb.AppendLine();
            sb.AppendLine(session.UsesConPty
                ? "Live console agent: the model watches the ConPTY console, then chooses the next console line or local window event."
                : "Live console agent: the model watches stdout/stderr, then chooses the next console line or local window event.");

            if (!session.Started)
            {
                sb.AppendLine($"The program did not start: {session.Error}");
                return sb.ToString();
            }

            return await AppendSessionReportAsync(sb, session, settings, requirements, plannerSource, identifiers, progress, cancellationToken);
        }

        private static async Task<string> AppendSessionReportAsync(StringBuilder sb, IInteractiveConsoleSession session,
            LLMSettings settings, string requirements, string plannerSource, IEnumerable<string>? identifiers,
            IProgress<ConsoleProgress>? progress, CancellationToken cancellationToken)
        {
            var drive = await ConsoleDriverAgent.DriveAsync(
                session,
                settings,
                requirements,
                plannerSource,
                progress,
                cancellationToken,
                identifiers);

            sb.AppendLine($"Testing time budget expired: {drive.TimedOut} | Stopped by runner: {drive.StoppedByRunner} | Crash: {drive.Crashed} | Infinite loop: {drive.InfiniteLoop} | Exit: {NativeExitCodes.Describe(drive.ExitCode)}");
            if (!string.IsNullOrWhiteSpace(drive.Error))
                sb.AppendLine($"Agent: {drive.Error}");
            if (NativeExitCodes.IsDllNotFound(drive.ExitCode))
            {
                sb.AppendLine("The process died before any console I/O. A required DLL was not found next to the executable.");
            }

            sb.AppendLine();
            sb.AppendLine("## Agent findings (crashes, hangs, failures)");
            if (drive.Findings.Count == 0)
            {
                sb.AppendLine("(none)");
            }
            else
            {
                foreach (var finding in drive.Findings)
                    sb.AppendLine("- " + finding);
            }

            sb.AppendLine();
            sb.AppendLine("## Console transcript");
            sb.AppendLine(string.IsNullOrWhiteSpace(drive.Transcript)
                ? "(empty)"
                : Sanitize(drive.Transcript, identifiers));

            if (!string.IsNullOrWhiteSpace(session.StandardOutput))
            {
                sb.AppendLine();
                sb.AppendLine("## Captured STDOUT (bounded tail)");
                sb.AppendLine(Sanitize(session.StandardOutput, identifiers));
            }

            if (!string.IsNullOrWhiteSpace(session.StandardError))
            {
                sb.AppendLine();
                sb.AppendLine("## Captured STDERR (bounded tail)");
                sb.AppendLine(Sanitize(session.StandardError, identifiers));
            }

            sb.AppendLine();
            sb.AppendLine("Use this live session as evidence of crashes, hangs, incorrect output, or weak validation.");
            sb.AppendLine("Do not invent runtime failures that are not listed.");
            sb.AppendLine("Inconclusive observations, runner limits, and unverified model opinions are not established student defects. Do not deduct points solely on these grounds.");

            return sb.ToString();
        }

        public async Task<string> BuildOnlyAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_settings.ExecutionMode == SubmissionExecutionMode.HyperV)
                return await BuildOrRunIsolatedAsync(solutionPath, false, cancellationToken);
            if (!SubmissionExecutionPolicy.IsLocalAuthorized(_settings, _confirmLocal, solutionPath))
                return SubmissionExecutionPolicy.LocalDeclined;

            var submission = RunnableSubmissionDetector.Detect(solutionPath);
            if (submission == null)
                return "No Visual Studio solution (.sln) was found.";

            var build = await BuildAsync(submission, cancellationToken);
            return build.Succeeded
                ? $"Build succeeded.\n{build.Log}".Trim()
                : $"Build failed.\n{build.Log}".Trim();
        }

        public async Task<string> LaunchAsync(string solutionPath, CancellationToken cancellationToken = default, bool buildFirst = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_settings.ExecutionMode == SubmissionExecutionMode.HyperV)
                return await BuildOrRunIsolatedAsync(solutionPath, true, cancellationToken, buildFirst);
            if (!SubmissionExecutionPolicy.IsLocalAuthorized(_settings, _confirmLocal, solutionPath))
                return SubmissionExecutionPolicy.LocalDeclined;

            var submission = RunnableSubmissionDetector.Detect(solutionPath);
            if (submission == null)
                return "No Visual Studio solution (.sln) was found.";

            var build = buildFirst
                ? await BuildAsync(submission, cancellationToken)
                : await ResolveExistingAsync(submission, cancellationToken);
            if (!build.Succeeded || string.IsNullOrWhiteSpace(build.CommandFileName))
                return $"Cannot run.\n{build.Log}".Trim();

            try
            {
                if (build.CommandFileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    NativeDependencyStager.CopyDependencies(build.CommandFileName, submission.RootDirectory);

                var startInfo = new ProcessStartInfo
                {
                    FileName = build.CommandFileName,
                    Arguments = build.Arguments,
                    WorkingDirectory = build.WorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                InteractiveProcessSession.PrependPath(
                    startInfo,
                    NativeDependencyStager.GetDllDirectories(submission.RootDirectory, build.CommandFileName));
                Process.Start(startInfo);
                return $"Started {Path.GetFileName(build.CommandFileName)}.\nProgram: {build.CommandFileName}\nWorking directory: {build.WorkingDirectory}";
            }
            catch (Exception ex)
            {
                return $"Failed to start the program: {ex.Message}";
            }
        }

        private async Task<string> BuildOrRunIsolatedAsync(string path, bool run, CancellationToken token, bool buildFirst = true)
        {
            try
            {
                var error = HyperVRunner.ConfigurationError(_settings);
                if (error != null) return error + "\n" + SubmissionExecutionPolicy.NoRuntimeDeduction;
                var submission = RunnableSubmissionDetector.Detect(path);
                if (submission == null) return "No runnable solution was found.";
                await using var runner = await HyperVRunner.CreateAsync(_settings, submission.RootDirectory, null, token);
                var entry = Path.GetRelativePath(submission.RootDirectory, submission.EntryPath);
                var build = buildFirst ? await runner.BuildAsync(entry, token) : await runner.ResolveExistingAsync(entry, token);
                if (!build.Succeeded || !run)
                    return buildFirst
                        ? $"Isolated build {(build.Succeeded ? "succeeded" : "failed")}.\n{build.Log}"
                        : $"Cannot run existing output in the VM.\n{build.Log}";
                await runner.StartAsync(token);
                if (!runner.Started) return "Guest program did not start: " + runner.Error;
                var deadline = DateTime.UtcNow.AddSeconds(90);
                while (!runner.HasExited && DateTime.UtcNow < deadline)
                    await runner.WaitForIdleAsync(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2), token);
                var exit = runner.HasExited ? "Exit: " + NativeExitCodes.Describe(runner.ExitCode)
                    : "Testing ended while the program was still running. Use Test with AI to answer interactive prompts. This is not evidence of a defect.";
                return $"Isolated run\n{exit}\n{runner.StandardOutput}\n{runner.StandardError}";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return "Isolated runner unavailable: " + ex.Message + "\n" + SubmissionExecutionPolicy.NoRuntimeDeduction;
            }
        }

        private static async Task<string> ExecuteIsolatedAsync(string path, string? searchRoot, string requirements,
            IEnumerable<string>? identifiers, IReadOnlyList<RelatedSubmissionFile>? relatedFiles,
            LLMSettings settings, IProgress<ConsoleProgress>? progress, CancellationToken token)
        {
            try
            {
                var error = HyperVRunner.ConfigurationError(settings);
                if (error != null) return error + "\n" + SubmissionExecutionPolicy.NoRuntimeDeduction;
                var submission = RunnableSubmissionDetector.Detect(path, searchRoot);
                if (submission == null) return "No runnable solution was found. " + SubmissionExecutionPolicy.NoRuntimeDeduction;
                var source = BuildPlannerSource(submission, relatedFiles, identifiers);
                await using var runner = await HyperVRunner.CreateAsync(settings, submission.RootDirectory, progress, token);
                var entry = Path.GetRelativePath(submission.RootDirectory, submission.EntryPath);
                var build = await runner.BuildAsync(entry, token);
                var report = new StringBuilder("# RUNTIME EXECUTION RESULTS\nEnvironment: isolated Hyper-V VM\n");
                report.AppendLine($"Build: {(build.Succeeded ? "succeeded" : "failed")}");
                report.AppendLine(Sanitize(build.Log, identifiers));
                if (!build.Succeeded)
                {
                    report.AppendLine("No program was run. Distinguish source errors from missing SDKs or offline dependencies before grading.");
                    return report.ToString();
                }
                await runner.StartAsync(token);
                if (!runner.Started) return report + "\nProgram did not start: " + runner.Error;
                return await AppendSessionReportAsync(report, runner, settings, requirements, source, identifiers, progress, token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return "Isolated runner unavailable: " + Sanitize(ex.Message, identifiers) + "\n" + SubmissionExecutionPolicy.NoRuntimeDeduction;
            }
        }

        internal static string BuildPlannerSource(
            RunnableSubmission submission,
            IReadOnlyList<RelatedSubmissionFile>? relatedFiles,
            IEnumerable<string>? identifiers)
        {
            var sb = new StringBuilder();
            var index = 1;
            var companions = (relatedFiles ?? Array.Empty<RelatedSubmissionFile>()).Take(8)
                .GroupBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            foreach (var sourcePath in submission.SourceFiles.Take(12))
            {
                try
                {
                    if (!File.Exists(sourcePath))
                        continue;

                    sb.AppendLine($"=== {StudentDataSanitizer.SafeDisplayName(Path.GetFileName(sourcePath), identifiers, index)} ===");
                    sb.AppendLine(StudentDataSanitizer.Sanitize(File.ReadAllText(sourcePath), identifiers));
                    index++;
                    // A solution file does not itself contain #includes. Resolve headers from its source files too.
                    if (companions.Count < 8)
                        foreach (var file in RelatedFileResolver.FindRelatedFiles(sourcePath, searchRoot: submission.RootDirectory))
                        {
                            if (companions.Count >= 8) break;
                            if (!submission.SourceFiles.Contains(file.FilePath, StringComparer.OrdinalIgnoreCase))
                                companions.TryAdd(file.FilePath, file);
                        }
                }
                catch
                {
                }
            }

            foreach (var related in companions.Values)
            {
                sb.AppendLine($"=== {StudentDataSanitizer.SafeDisplayName(related.FileName, identifiers, index)} ===");
                sb.AppendLine(StudentDataSanitizer.Sanitize(related.Content, identifiers));
                index++;
            }

            return sb.ToString();
        }

        private static RunnableSubmission StageSubmission(RunnableSubmission submission)
        {
            var dest = AiTestStaging.StageWithSharedDependencies(submission.RootDirectory);
            var stagedEntry = AiTestStaging.RemapPath(submission.EntryPath, submission.RootDirectory, dest);
            var staged = RunnableSubmissionDetector.Detect(stagedEntry);
            if (staged == null)
                throw new InvalidOperationException($"Staged solution was not found at {stagedEntry}.");

            return staged;
        }

        private static string Sanitize(string text, IEnumerable<string>? identifiers)
        {
            return StudentDataSanitizer.Sanitize(text, identifiers);
        }

    }
}
