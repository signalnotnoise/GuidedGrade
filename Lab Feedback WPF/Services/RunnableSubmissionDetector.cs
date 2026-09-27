using System.IO;

namespace Lab_Feedback_WPF.Services
{
    public enum SubmissionKind
    {
        None,
        VisualStudioSolution,
        DotNetProject,
        Python,
        Java,
        CppProject,
        CppSources,
        NativeExecutable
    }

    public sealed class RunnableSubmission
    {
        public SubmissionKind Kind { get; init; }
        public string RootDirectory { get; init; } = string.Empty;
        public string EntryPath { get; init; } = string.Empty;
        public IReadOnlyList<string> SourceFiles { get; init; } = Array.Empty<string>();
    }

    public static class RunnableSubmissionDetector
    {
        private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
        {
            ".vs", "bin", "obj", "Debug", "Release", "x64", "x86", ".git", ".lab-feedback-run"
        };

        public static bool IsSolutionFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var extension = Path.GetExtension(path);
            return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
        }

        public static RunnableSubmission? Detect(string primaryPath, string? searchRoot = null)
        {
            if (string.IsNullOrWhiteSpace(primaryPath))
                return null;

            var fullPrimary = Path.GetFullPath(primaryPath);
            if (IsSolutionFile(fullPrimary) && File.Exists(fullPrimary))
                return FromSolution(fullPrimary);

            var root = ResolveRoot(fullPrimary, searchRoot);
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return null;

            var solution = EnumerateFiles(root, "*.sln")
                .Concat(EnumerateFiles(root, "*.slnx"))
                .FirstOrDefault();

            return solution == null ? null : FromSolution(solution);
        }

        private static RunnableSubmission FromSolution(string solutionPath)
        {
            var root = Path.GetDirectoryName(solutionPath) ?? Path.GetFullPath(solutionPath);
            return new RunnableSubmission
            {
                Kind = SubmissionKind.VisualStudioSolution,
                RootDirectory = root,
                EntryPath = solutionPath,
                SourceFiles = EnumerateSourceFiles(root)
            };
        }

        private static string ResolveRoot(string fullPrimary, string? searchRoot)
        {
            if (!string.IsNullOrWhiteSpace(searchRoot) && Directory.Exists(searchRoot))
                return Path.GetFullPath(searchRoot);

            return File.Exists(fullPrimary)
                ? Path.GetDirectoryName(fullPrimary) ?? fullPrimary
                : fullPrimary;
        }

        private static List<string> EnumerateSourceFiles(string root)
        {
            return EnumerateFiles(root, "*.c")
                .Concat(EnumerateFiles(root, "*.cc"))
                .Concat(EnumerateFiles(root, "*.cpp"))
                .Concat(EnumerateFiles(root, "*.cxx"))
                .Concat(EnumerateFiles(root, "*.cs"))
                .Concat(EnumerateFiles(root, "*.py"))
                .Concat(EnumerateFiles(root, "*.java"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();
        }

        internal static IEnumerable<string> EnumerateFiles(string root, string pattern)
        {
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                string[] files;
                try
                {
                    files = Directory.GetFiles(directory, pattern);
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                    yield return file;

                string[] subdirectories;
                try
                {
                    subdirectories = Directory.GetDirectories(directory);
                }
                catch
                {
                    continue;
                }

                foreach (var subdirectory in subdirectories)
                {
                    if (!ExcludedDirectories.Contains(Path.GetFileName(subdirectory)))
                        pending.Push(subdirectory);
                }
            }
        }

        internal static string? FindPrimaryVcxproj(string root, string? preferredName = null)
        {
            var projects = EnumerateFiles(root, "*.vcxproj").ToList();
            if (projects.Count == 0)
                return null;

            var nested = projects
                .Where(path => !string.Equals(Path.GetDirectoryName(path), root, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var pool = nested.Count > 0 ? nested : projects;

            if (!string.IsNullOrWhiteSpace(preferredName))
            {
                var exact = pool.FirstOrDefault(path =>
                    Path.GetFileNameWithoutExtension(path).Equals(preferredName, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                    return exact;

                var contained = pool.FirstOrDefault(path =>
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    return preferredName.Contains(name, StringComparison.OrdinalIgnoreCase)
                        || name.Contains(preferredName, StringComparison.OrdinalIgnoreCase);
                });
                if (contained != null)
                    return contained;
            }

            return pool[0];
        }

        internal static string InferNativePlatform(string projectDirectory)
        {
            foreach (var platform in new[] { "x64", "X64", "Win32", "x86" })
            {
                if (Directory.Exists(Path.Combine(projectDirectory, platform)))
                    return platform.Equals("X64", StringComparison.OrdinalIgnoreCase) ? "x64" : platform;
            }

            return "x64";
        }

        internal static string GetNativeOutDir(string projectDirectory, string platform, string configuration = "Debug")
        {
            return Path.Combine(projectDirectory, platform, configuration)
                + Path.DirectorySeparatorChar;
        }

        internal static string? FindNewestExecutable(string root, string? preferredName = null)
        {
            return FindRunnableOutput(root, preferredName, allowManagedDll: false);
        }

        internal static string? FindRunnableOutput(string root, string? preferredName = null, bool allowManagedDll = true)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return null;

            try
            {
                var candidates = new List<string>();

                foreach (var config in Directory.GetFiles(root, "*.runtimeconfig.json", SearchOption.AllDirectories))
                {
                    if (IsExcludedPath(config) || IsToolAssembly(config))
                        continue;

                    var directory = Path.GetDirectoryName(config);
                    if (string.IsNullOrEmpty(directory))
                        continue;

                    var baseName = Path.GetFileName(config);
                    const string suffix = ".runtimeconfig.json";
                    if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        baseName = baseName.Substring(0, baseName.Length - suffix.Length);

                    var exe = Path.Combine(directory, baseName + ".exe");
                    var dll = Path.Combine(directory, baseName + ".dll");
                    if (File.Exists(exe) && !IsToolAssembly(exe))
                        candidates.Add(exe);
                    else if (allowManagedDll && File.Exists(dll) && !IsToolAssembly(dll))
                        candidates.Add(dll);
                }

                foreach (var exe in Directory.GetFiles(root, "*.exe", SearchOption.AllDirectories))
                {
                    if (!IsExcludedPath(exe) && !IsToolAssembly(exe))
                        candidates.Add(exe);
                }

                candidates = candidates
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (candidates.Count == 0)
                    return null;

                if (!string.IsNullOrWhiteSpace(preferredName))
                {
                    var preferred = candidates
                        .Where(path => Path.GetFileNameWithoutExtension(path)
                            .Equals(preferredName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (preferred.Count > 0)
                        candidates = preferred;
                }

                return candidates
                    .OrderByDescending(HasSiblingDlls)
                    .ThenByDescending(OutputPathDepth)
                    .ThenByDescending(IsProjectOutputPath)
                    .ThenByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static bool HasSiblingDlls(string path)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                return !string.IsNullOrEmpty(directory)
                    && Directory.GetFiles(directory, "*.dll").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static int OutputPathDepth(string path)
        {
            return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;
        }

        private static bool IsProjectOutputPath(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Any(part =>
                part.Equals("Debug", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Release", StringComparison.OrdinalIgnoreCase)
                || part.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || part.Equals("x64", StringComparison.OrdinalIgnoreCase)
                || part.Equals("x86", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Win32", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsExcludedPath(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Any(part =>
                part.Equals(".lab-feedback-run", StringComparison.OrdinalIgnoreCase)
                || part.Equals(".git", StringComparison.OrdinalIgnoreCase)
                || part.Equals(".vs", StringComparison.OrdinalIgnoreCase)
                || part.Equals("ref", StringComparison.OrdinalIgnoreCase)
                || part.Equals("packages", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsToolAssembly(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.EndsWith(".vshost", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return name.Equals("testhost", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("testhost.", StringComparison.OrdinalIgnoreCase)
                || name.Equals("vstest.console", StringComparison.OrdinalIgnoreCase)
                || name.Equals("MSBuild", StringComparison.OrdinalIgnoreCase)
                || name.Equals("VBCSCompiler", StringComparison.OrdinalIgnoreCase)
                || name.Equals("csc", StringComparison.OrdinalIgnoreCase)
                || name.Equals("vbc", StringComparison.OrdinalIgnoreCase)
                || name.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                || name.Equals("conhost", StringComparison.OrdinalIgnoreCase);
        }
    }
}
