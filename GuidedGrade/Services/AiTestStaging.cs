using System.IO;
using System.Text;
using System.Xml.Linq;

namespace GuidedGrade.Services
{
    public static class AiTestStaging
    {
        public const string DefaultRoot = @"C:\aitest";

        private static readonly HashSet<string> SkipDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ".vs", ".git", ".guidedgrade-run", "bin", "obj", "ipch", ".tlog"
        };

        private static readonly HashSet<string> SkipFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".tlog", ".lastbuildstate", ".unsuccessfulbuild", ".idb", ".ilk", ".pdb",
            ".obj", ".iobj", ".ipdb", ".pch", ".ipch", ".sbr", ".bsc", ".res"
        };

        private static readonly HashSet<string> PathRewriteExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".sln", ".slnx", ".vcxproj", ".vcxproj.filters", ".vcxproj.user",
            ".csproj", ".fsproj", ".vbproj", ".props", ".targets", ".filters"
        };

        public static string Stage(string sourceDirectory, string destinationRoot = DefaultRoot, bool preserveBinaryLibraries = false)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException($"Submission directory was not found: {sourceDirectory}");

            var source = Path.GetFullPath(sourceDirectory);
            var root = Path.GetFullPath(destinationRoot);
            var leaf = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(leaf))
                leaf = "submission";

            // Keep the original folder name so $(SolutionDir)/$(ProjectDir) stay one
            // level deeper than C:\aitest. Flattening into C:\aitest puts the exe
            // above the project Debug folder that holds the DLLs.
            var destination = Path.Combine(root, leaf);

            Directory.CreateDirectory(root);

            if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            {
                // Wipe any leftover copy of this submission (including an existing
                // .sln) so incremental MSBuild cannot reuse object files that still
                // point at the original source tree. Sibling folders under C:\aitest
                // are left alone in case another run still holds them open.
                RemoveDestination(destination);
                Directory.CreateDirectory(destination);
                CopyDirectory(source, destination, preserveBinaryLibraries);
                RewriteOriginalPaths(source, destination);
            }

            return destination;
        }

        internal static string StageWithSharedDependencies(string sourceDirectory, string destinationRoot = DefaultRoot)
        {
            var source = Path.GetFullPath(sourceDirectory);
            var parent = Path.GetDirectoryName(source);
            var shared = parent == null ? null : Path.Combine(parent, "Shared");
            var referenced = shared != null && Directory.Exists(shared) &&
                Directory.EnumerateFiles(source, "*.vcxproj", SearchOption.AllDirectories).Any(project =>
                    XDocument.Load(project).Descendants().Where(element => element.Name.LocalName == "Import")
                    .Select(element => (string?)element.Attribute("Project"))
                    .Where(path => !string.IsNullOrWhiteSpace(path) && !path.Contains('$') && !Path.IsPathRooted(path))
                    .Any(path => ReviewContext.Contains(shared, Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, path!)))));
            if (!referenced) return Stage(source, destinationRoot);
            // Each run owns its Week1/Shared pair; no other repository folders
            // are copied and dependencies cannot be mixed between students.
            var runRoot = Path.Combine(destinationRoot, "run-" + Guid.NewGuid().ToString("N"));
            Stage(shared!, runRoot, preserveBinaryLibraries: true);
            return Stage(source, runRoot);
        }

        public static string RemapPath(string originalPath, string originalRoot, string stagedRoot)
        {
            var full = Path.GetFullPath(originalPath);
            var root = Path.GetFullPath(originalRoot);
            var dest = Path.GetFullPath(stagedRoot);

            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                var relative = full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.IsNullOrEmpty(relative) ? dest : Path.Combine(dest, relative);
            }

            return Path.Combine(dest, Path.GetFileName(full));
        }

        private static void RemoveDestination(string destination)
        {
            if (!Directory.Exists(destination) && !File.Exists(destination))
                return;

            try
            {
                if (File.Exists(destination))
                {
                    ClearReadOnly(destination);
                    File.Delete(destination);
                    return;
                }

                ClearReadOnlyRecursive(destination);
                Directory.Delete(destination, true);
            }
            catch
            {
                if (Directory.Exists(destination))
                    ClearDirectory(destination);
            }
        }

        private static void ClearDirectory(string directory)
        {
            foreach (var file in Directory.GetFiles(directory))
            {
                try
                {
                    ClearReadOnly(file);
                    File.Delete(file);
                }
                catch
                {
                }
            }

            foreach (var sub in Directory.GetDirectories(directory))
            {
                try
                {
                    ClearReadOnlyRecursive(sub);
                    Directory.Delete(sub, true);
                }
                catch
                {
                    ClearDirectory(sub);
                }
            }
        }

        private static void CopyDirectory(string source, string destination, bool preserveBinaryLibraries)
        {
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                if (ShouldSkip(directory, source, preserveBinaryLibraries))
                    continue;

                Directory.CreateDirectory(RemapPath(directory, source, destination));
            }

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (ShouldSkip(file, source, preserveBinaryLibraries))
                    continue;

                var target = RemapPath(file, source, destination);
                var targetDir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(targetDir))
                    Directory.CreateDirectory(targetDir);

                ClearReadOnly(target);
                File.Copy(file, target, true);
            }
        }

        private static void RewriteOriginalPaths(string sourceRoot, string destinationRoot)
        {
            var original = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var staged = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(original, staged, StringComparison.OrdinalIgnoreCase))
                return;

            foreach (var file in Directory.GetFiles(destinationRoot, "*", SearchOption.AllDirectories))
            {
                if (!PathRewriteExtensions.Contains(Path.GetExtension(file)))
                    continue;

                try
                {
                    var text = File.ReadAllText(file);
                    var updated = ReplacePathVariants(text, original, staged);
                    if (!string.Equals(text, updated, StringComparison.Ordinal))
                        File.WriteAllText(file, updated, Encoding.UTF8);
                }
                catch
                {
                }
            }
        }

        private static string ReplacePathVariants(string text, string originalRoot, string stagedRoot)
        {
            return text
                .Replace(originalRoot, stagedRoot, StringComparison.OrdinalIgnoreCase)
                .Replace(originalRoot.Replace('\\', '/'), stagedRoot.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)
                .Replace(originalRoot.Replace('/', '\\'), stagedRoot.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkip(string path, string sourceRoot, bool preserveBinaryLibraries)
        {
            var relative = Path.GetFullPath(path).Substring(Path.GetFullPath(sourceRoot).Length);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var library = preserveBinaryLibraries && File.Exists(path) &&
                (Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".lib", StringComparison.OrdinalIgnoreCase));
            if (parts.Any(part => SkipDirectoryNames.Contains(part) && !(library && part.Equals("bin", StringComparison.OrdinalIgnoreCase))))
                return true;

            return File.Exists(path) && SkipFileExtensions.Contains(Path.GetExtension(path));
        }

        private static void ClearReadOnlyRecursive(string path)
        {
            ClearReadOnly(path);
            if (!Directory.Exists(path))
                return;

            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                ClearReadOnly(file);

            foreach (var directory in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
                ClearReadOnly(directory);
        }

        private static void ClearReadOnly(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
                return;

            try
            {
                var attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
            }
            catch
            {
            }
        }
    }
}
