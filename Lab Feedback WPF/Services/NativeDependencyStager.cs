using System.IO;

namespace Lab_Feedback_WPF.Services
{
    public static class NativeDependencyStager
    {
        public static int CopyDependencies(string executablePath, string searchRoot)
        {
            var exeDir = Path.GetDirectoryName(Path.GetFullPath(executablePath));
            if (string.IsNullOrEmpty(exeDir) || !Directory.Exists(searchRoot))
                return 0;

            Directory.CreateDirectory(exeDir);
            var copied = 0;

            foreach (var dll in EnumerateDlls(searchRoot).OrderByDescending(DllPriority))
            {
                var dest = Path.Combine(exeDir, Path.GetFileName(dll));
                if (string.Equals(Path.GetFullPath(dll), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                    continue;
                // Never replace the successful build's assemblies with an older or
                // reference-only assembly discovered elsewhere in the submission.
                if (File.Exists(dest))
                    continue;

                try
                {
                    File.Copy(dll, dest, overwrite: false);
                    copied++;
                }
                catch
                {
                }
            }

            return copied;
        }

        public static IReadOnlyList<string> GetDllDirectories(string searchRoot, string? executablePath = null)
        {
            var dirs = new List<string>();
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                var exeDir = Path.GetDirectoryName(Path.GetFullPath(executablePath));
                if (!string.IsNullOrEmpty(exeDir))
                    dirs.Add(exeDir);
            }

            if (Directory.Exists(searchRoot))
            {
                dirs.AddRange(EnumerateDlls(searchRoot)
                    .Select(Path.GetDirectoryName)
                    .Where(dir => !string.IsNullOrEmpty(dir))
                    .Cast<string>());
            }

            return dirs
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<string> EnumerateDlls(string searchRoot)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.GetFiles(searchRoot, "*.dll", SearchOption.AllDirectories);
            }
            catch
            {
                yield break;
            }

            foreach (var file in files)
            {
                var parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (parts.Any(part =>
                        part.Equals(".vs", StringComparison.OrdinalIgnoreCase)
                        || part.Equals(".git", StringComparison.OrdinalIgnoreCase)
                        || part.Equals("packages", StringComparison.OrdinalIgnoreCase)
                        || part.Equals("obj", StringComparison.OrdinalIgnoreCase)
                        || part.Equals("refint", StringComparison.OrdinalIgnoreCase)
                        || part.Equals("ref", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                yield return file;
            }
        }

        private static int DllPriority(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var score = 0;
            if (parts.Any(part => part.Equals("x64", StringComparison.OrdinalIgnoreCase)))
                score += 2;
            if (parts.Any(part => part.Equals("Debug", StringComparison.OrdinalIgnoreCase)))
                score += 1;
            return score;
        }
    }
}
