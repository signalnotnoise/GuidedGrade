using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GuidedGrade.Services
{
    public class SubmissionBuilder
    {
        private static readonly TimeSpan BuildTimeout = TimeSpan.FromSeconds(90);
        internal delegate Task<ProcessRunResult> RunProcess(
            string fileName, string arguments, string? workingDirectory, string? stdin,
            TimeSpan timeout, CancellationToken cancellationToken);
        private readonly RunProcess _runProcess;
        private readonly Func<string, CancellationToken, Task<string?>>? _toolResolver;

        public SubmissionBuilder() : this(ProcessRunner.RunAsync, null) { }

        internal SubmissionBuilder(RunProcess runProcess,
            Func<string, CancellationToken, Task<string?>>? toolResolver)
        {
            _runProcess = runProcess;
            _toolResolver = toolResolver;
        }

        internal async Task<BuildResult> BuildAsync(RunnableSubmission submission, CancellationToken cancellationToken)
        {
            var outputDir = Path.Combine(Path.GetTempPath(), "GuidedGradeRun", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputDir);

            return submission.Kind switch
            {
                SubmissionKind.VisualStudioSolution => await BuildVisualStudioSolutionAsync(submission, cancellationToken),
                SubmissionKind.Python => await BuildPythonAsync(submission, cancellationToken),
                SubmissionKind.DotNetProject => await BuildDotNetAsync(submission, cancellationToken),
                SubmissionKind.Java => await BuildJavaAsync(submission, outputDir, cancellationToken),
                SubmissionKind.CppProject => await BuildCppProjectAsync(submission, cancellationToken),
                SubmissionKind.CppSources => await BuildCppSourcesAsync(submission, outputDir, cancellationToken),
                SubmissionKind.NativeExecutable => new BuildResult(false, "", "", submission.RootDirectory, "Prebuilt executables cannot establish whether submitted source builds correctly."),
                _ => new BuildResult(false, "", "", submission.RootDirectory, "Unsupported submission kind.")
            };
        }

        internal async Task<BuildResult> ResolveExistingAsync(RunnableSubmission submission, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return await ResolveRunnableAsync(submission, submission.RootDirectory, token)
                ?? new BuildResult(false, "", "", submission.RootDirectory,
                    "No existing runnable output was found. Build the solution first; Run does not compile source.");
        }

        private async Task<BuildResult> BuildVisualStudioSolutionAsync(
            RunnableSubmission submission,
            CancellationToken cancellationToken)
        {
            var outputDir = Path.Combine(submission.RootDirectory, "bin", "GuidedGrade");
            Directory.CreateDirectory(outputDir);

            var logs = new StringBuilder();
            string? dotnet = null;

            var msbuild = await FindMsBuildAsync(cancellationToken);
            if (msbuild != null)
            {
                // Put the exe in the .vcxproj folder (RPG_Shop\x64\Debug), not
                // $(SolutionDir)\X64\Debug, so sibling DLLs resolve. Rebuild avoids
                // reusing stale intermediates; AI tests supply a staged copy.
                var build = await _runProcess(
                    msbuild,
                    BuildMsBuildArguments(submission),
                    submission.RootDirectory,
                    null,
                    BuildTimeout,
                    cancellationToken);

                logs.AppendLine(Combine(build));
                if (build.TimedOut)
                    return new BuildResult(false, "", "", submission.RootDirectory, logs.ToString().Trim());
                if (build.Started && !build.TimedOut && build.ExitCode == 0)
                {
                    var runnable = await ResolveRunnableAsync(submission, submission.RootDirectory, cancellationToken);
                    if (runnable != null)
                        return runnable with { Log = logs.ToString().Trim() };
                }
            }

            // A second toolchain cannot recover a timed-out build or a native MSBuild failure.
            // In particular, do not clean an executable just linked before a failed post-build step.
            if (RunnableSubmissionDetector.FindPrimaryVcxproj(submission.RootDirectory,
                    Path.GetFileNameWithoutExtension(submission.EntryPath)) != null)
                return new BuildResult(false, "", "", submission.RootDirectory,
                    logs.Length == 0 ? "Visual Studio C++ build tools were not found." : logs.ToString().Trim());

            dotnet = await FindToolAsync("dotnet", cancellationToken);
            if (dotnet != null)
            {
                await _runProcess(
                    dotnet,
                    $"clean \"{submission.EntryPath}\" -c Debug --nologo",
                    submission.RootDirectory,
                    null,
                    BuildTimeout,
                    cancellationToken);

                var build = await _runProcess(
                    dotnet,
                    $"build \"{submission.EntryPath}\" -c Debug -o \"{outputDir}\" --nologo --no-incremental",
                    submission.RootDirectory,
                    null,
                    BuildTimeout,
                    cancellationToken);

                logs.AppendLine(Combine(build));
                if (build.Started && !build.TimedOut && build.ExitCode == 0)
                {
                    var runnable = await ResolveRunnableAsync(submission, outputDir, cancellationToken, dotnet);
                    if (runnable != null)
                        return runnable with { Log = logs.ToString().Trim() };

                    return new BuildResult(
                        false,
                        "",
                        "",
                        submission.RootDirectory,
                        logs + "\nBuild succeeded but no runnable program (.exe or .dll) was found.");
                }
            }

            return new BuildResult(
                false,
                "",
                "",
                submission.RootDirectory,
                string.IsNullOrWhiteSpace(logs.ToString())
                    ? "Could not build or locate a runnable program for the solution."
                    : logs.ToString().Trim());
        }

        internal static string BuildMsBuildArguments(RunnableSubmission submission)
        {
            var args = $"\"{submission.EntryPath}\" /t:Rebuild /p:Configuration=Debug /v:minimal /nologo";
            var vcxproj = RunnableSubmissionDetector.FindPrimaryVcxproj(
                submission.RootDirectory,
                Path.GetFileNameWithoutExtension(submission.EntryPath));
            if (vcxproj == null)
                return args;

            var projectDir = Path.GetDirectoryName(vcxproj);
            if (string.IsNullOrEmpty(projectDir))
                return args;

            var platform = RunnableSubmissionDetector.InferNativePlatform(projectDir);
            // Native post-build tools such as xcopy require Windows separators.
            // Double the trailing backslash for Windows argument parsing before a quote.
            var outDir = RunnableSubmissionDetector.GetNativeOutDir(projectDir, platform)
                .Replace('/', '\\').TrimEnd('\\') + "\\\\";
            // Preserve each project's intermediate directory. A global IntDir
            // merges compiler PDB/object files across projects. Serialize project
            // builds as submitted projects may themselves share output locations.
            string command = $"{args} /p:Platform={platform} /p:OutDir=\"{outDir}\" /m:1";
            return command;
        }

        private async Task<BuildResult?> ResolveRunnableAsync(
            RunnableSubmission submission,
            string searchRoot,
            CancellationToken cancellationToken,
            string? dotnet = null)
        {
            var preferredName = Path.GetFileNameWithoutExtension(submission.EntryPath);
            var program = RunnableSubmissionDetector.FindRunnableOutput(searchRoot, preferredName);

            if (string.IsNullOrWhiteSpace(program))
                return null;

            var workingDirectory = ResolveWorkingDirectory(submission, program);
            if (program.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return new BuildResult(true, program, "", workingDirectory, "");
            }

            dotnet ??= await FindToolAsync("dotnet", cancellationToken);
            if (dotnet == null)
                return null;

            return new BuildResult(true, dotnet, $"exec \"{program}\"", workingDirectory, "");
        }

        internal static string ResolveWorkingDirectory(RunnableSubmission submission, string program)
        {
            // Native Visual Studio programs commonly open source/data files relative
            // to the project directory (the debugger default), not x64/Debug.
            // DLL staging still uses the executable directory independently.
            var project = submission.Kind == SubmissionKind.CppProject
                ? submission.EntryPath
                : submission.Kind == SubmissionKind.VisualStudioSolution
                    ? FindSolutionProjectForProgram(submission.EntryPath, program)
                        ?? RunnableSubmissionDetector.FindPrimaryVcxproj(submission.RootDirectory,
                        Path.GetFileNameWithoutExtension(program))
                    : null;
            return project != null
                ? Path.GetDirectoryName(project) ?? submission.RootDirectory
                : Path.GetDirectoryName(program) ?? submission.RootDirectory;
        }

        private static string? FindSolutionProjectForProgram(string solution, string program)
        {
            if (!solution.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || !File.Exists(solution)) return null;
            var root = Path.GetDirectoryName(solution)!;
            var executableName = Path.GetFileNameWithoutExtension(program);
            // A solution display name can match the executable while differing
            // from the vcxproj filename (e.g. Lab1 -> CaveMatchingGame).
            foreach (var line in File.ReadLines(solution))
            {
                var match = Regex.Match(line, "^\\s*Project\\(\"[^\"]+\"\\)\\s*=\\s*\"([^\"]+)\",\\s*\"([^\"]+\\.vcxproj)\"", RegexOptions.IgnoreCase);
                if (!match.Success || !string.Equals(match.Groups[1].Value, executableName, StringComparison.OrdinalIgnoreCase)) continue;
                var project = Path.GetFullPath(Path.Combine(root, match.Groups[2].Value));
                if (File.Exists(project)) return project;
            }
            return null;
        }

        private async Task<BuildResult> BuildPythonAsync(RunnableSubmission submission, CancellationToken cancellationToken)
        {
            var python = await FindToolAsync("py", cancellationToken)
                ?? await FindToolAsync("python", cancellationToken)
                ?? await FindToolAsync("python3", cancellationToken);
            if (python == null)
                return new BuildResult(false, "", "", submission.RootDirectory, "Python interpreter not found on PATH.");

            var arguments = python.EndsWith("py.exe", StringComparison.OrdinalIgnoreCase)
                ? $"-3 \"{submission.EntryPath}\""
                : $"\"{submission.EntryPath}\"";

            return new BuildResult(true, python, arguments, submission.RootDirectory, "Python script is ready to run.");
        }

        private async Task<BuildResult> BuildDotNetAsync(RunnableSubmission submission, CancellationToken cancellationToken)
        {
            var dotnet = await FindToolAsync("dotnet", cancellationToken);
            if (dotnet == null)
                return new BuildResult(false, "", "", submission.RootDirectory, "dotnet SDK not found on PATH.");

            await _runProcess(
                dotnet,
                $"clean \"{submission.EntryPath}\" -c Debug --nologo",
                submission.RootDirectory,
                null,
                BuildTimeout,
                cancellationToken);

            var build = await _runProcess(
                dotnet,
                $"build \"{submission.EntryPath}\" -c Debug --nologo --no-incremental",
                submission.RootDirectory,
                null,
                BuildTimeout,
                cancellationToken);

            var log = Combine(build);
            if (!build.Started || build.TimedOut || build.ExitCode != 0)
                return new BuildResult(false, "", "", submission.RootDirectory, log);

            var runnable = await ResolveRunnableAsync(submission, submission.RootDirectory, cancellationToken, dotnet);
            if (runnable != null)
                return runnable with { Log = log };

            return new BuildResult(
                false,
                "",
                "",
                submission.RootDirectory,
                log + "\nBuild succeeded but no runnable program (.exe or .dll) was found.");
        }

        private async Task<BuildResult> BuildJavaAsync(
            RunnableSubmission submission,
            string outputDir,
            CancellationToken cancellationToken)
        {
            var javac = await FindToolAsync("javac", cancellationToken);
            var java = await FindToolAsync("java", cancellationToken);
            if (javac == null || java == null)
                return new BuildResult(false, "", "", submission.RootDirectory, "javac/java not found on PATH.");

            var sources = string.Join(" ", submission.SourceFiles.Select(path => $"\"{path}\""));
            var build = await _runProcess(
                javac,
                $"-d \"{outputDir}\" {sources}",
                submission.RootDirectory,
                null,
                BuildTimeout,
                cancellationToken);

            var log = Combine(build);
            if (!build.Started || build.TimedOut || build.ExitCode != 0)
                return new BuildResult(false, "", "", submission.RootDirectory, log);

            var className = Path.GetFileNameWithoutExtension(submission.EntryPath);
            return new BuildResult(true, java, $"-cp \"{outputDir}\" {className}", submission.RootDirectory, log);
        }

        private async Task<BuildResult> BuildCppProjectAsync(RunnableSubmission submission, CancellationToken cancellationToken)
        {
            var msbuild = await FindMsBuildAsync(cancellationToken);
            if (msbuild != null)
            {
                var build = await _runProcess(
                    msbuild,
                    $"\"{submission.EntryPath}\" /t:Rebuild /p:Configuration=Debug /m:1 /v:minimal",
                    submission.RootDirectory,
                    null,
                    BuildTimeout,
                    cancellationToken);

                var log = Combine(build);
                var exe = RunnableSubmissionDetector.FindNewestExecutable(submission.RootDirectory);
                if (build.Started && !build.TimedOut && build.ExitCode == 0 && exe != null)
                    return new BuildResult(true, exe, "", ResolveWorkingDirectory(submission, exe), log);

                return new BuildResult(false, "", "", submission.RootDirectory, log);
            }

            return await BuildCppSourcesAsync(submission, Path.Combine(Path.GetTempPath(), "GuidedGradeRun", Guid.NewGuid().ToString("N")), cancellationToken);
        }

        private async Task<BuildResult> BuildCppSourcesAsync(
            RunnableSubmission submission,
            string outputDir,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(outputDir);
            var outputExe = Path.Combine(outputDir, "student.exe");
            var sources = string.Join(" ", submission.SourceFiles.Select(path => $"\"{path}\""));
            if (string.IsNullOrWhiteSpace(sources))
                sources = $"\"{submission.EntryPath}\"";

            var gpp = await FindToolAsync("g++", cancellationToken) ?? await FindToolAsync("clang++", cancellationToken);
            if (gpp != null)
            {
                var build = await _runProcess(
                    gpp,
                    $"-std=c++17 -O0 -o \"{outputExe}\" {sources}",
                    submission.RootDirectory,
                    null,
                    BuildTimeout,
                    cancellationToken);
                var log = Combine(build);
                if (build.Started && !build.TimedOut && build.ExitCode == 0 && File.Exists(outputExe))
                    return new BuildResult(true, outputExe, "", outputDir, log);

                return new BuildResult(false, "", "", submission.RootDirectory, log);
            }

            var cl = await FindToolAsync("cl", cancellationToken);
            if (cl != null)
            {
                var build = await _runProcess(
                    cl,
                    $"/nologo /EHsc /Fe:\"{outputExe}\" {sources}",
                    submission.RootDirectory,
                    null,
                    BuildTimeout,
                    cancellationToken);
                var log = Combine(build);
                if (build.Started && !build.TimedOut && build.ExitCode == 0 && File.Exists(outputExe))
                    return new BuildResult(true, outputExe, "", outputDir, log);

                return new BuildResult(false, "", "", submission.RootDirectory, log);
            }

            return new BuildResult(false, "", "", submission.RootDirectory, "No C++ compiler (g++, clang++, cl, or MSBuild) was found.");
        }

        private async Task<string?> FindMsBuildAsync(CancellationToken cancellationToken)
        {
            if (_toolResolver != null)
                return await _toolResolver("msbuild", cancellationToken);
            var vswhere = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft Visual Studio", "Installer", "vswhere.exe");

            if (File.Exists(vswhere))
            {
                var result = await _runProcess(
                    vswhere,
                    "-latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe",
                    null,
                    null,
                    TimeSpan.FromSeconds(15),
                    cancellationToken);

                var path = result.StandardOutput
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(File.Exists);
                if (path != null)
                    return path;
            }

            return await FindToolAsync("msbuild", cancellationToken);
        }

        private async Task<string?> FindToolAsync(string name, CancellationToken cancellationToken)
        {
            if (_toolResolver != null)
                return await _toolResolver(name, cancellationToken);
            var whereExe = Path.Combine(Environment.SystemDirectory, "where.exe");
            if (!File.Exists(whereExe))
                return null;

            var result = await _runProcess(
                whereExe,
                name,
                null,
                null,
                TimeSpan.FromSeconds(5),
                cancellationToken);

            return result.StandardOutput
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(File.Exists);
        }

        private static string Combine(ProcessRunResult result)
        {
            var sb = new StringBuilder();
            var output = result.StandardOutput + result.StandardError;
            if (output.Contains("LNK1104", StringComparison.OrdinalIgnoreCase) &&
                output.Contains(".exe", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine("The linker could not open an executable. Close the previously launched submission's terminal/game window (or end that submission in Task Manager), then rebuild. A running executable or its loaded DLLs can also block post-build copies. If no copy is running, check the reported path's permissions and file locks.");
            if ((result.StandardOutput + result.StandardError).Contains("C1041", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine("Compiler PDB access failed (C1041). This is a build-file locking/configuration issue, not evidence of a source-code defect. Close other builds/debuggers using this submission and retry. If it persists in a synced folder, build a local copy outside OneDrive. Ensure projects use separate intermediate directories and enable /FS in C/C++ > Command Line > Additional Options.");
            if (result.TimedOut)
                sb.AppendLine("Build stopped after the time limit. A build step may be waiting for input; build commands must run unattended. A timeout alone is not evidence of a student-code defect.");
            if (result.StandardOutput.Contains("(F = file, D = directory)", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine("A post-build copy step requested a file/directory choice. Use an existing destination directory with Windows backslashes, or xcopy /I for directory copies.");
            if (!string.IsNullOrWhiteSpace(result.Error))
                sb.AppendLine(result.Error);
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                sb.AppendLine(result.StandardOutput);
            if (!string.IsNullOrWhiteSpace(result.StandardError))
                sb.AppendLine(result.StandardError);
            return sb.ToString().Trim();
        }

        internal sealed record BuildResult(
            bool Succeeded,
            string CommandFileName,
            string Arguments,
            string WorkingDirectory,
            string Log);
    }
}
