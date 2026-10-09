using System.IO;
using System.Text.RegularExpressions;

namespace GuidedGrade.Services
{
    public sealed class RelatedSubmissionFile
    {
        public string FileName { get; init; } = "related.cpp";
        public string FilePath { get; init; } = string.Empty;
        public string Extension { get; init; } = ".cpp";
        public string Content { get; init; } = string.Empty;
        public IReadOnlyList<string> DeclaredTypes { get; init; } = Array.Empty<string>();
    }

    public static class RelatedFileResolver
    {
        private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".c", ".cc", ".cpp", ".cxx", ".h", ".hh", ".hpp", ".hxx", ".cs", ".java", ".py", ".js", ".ts"
        };

        private static readonly HashSet<string> ExcludedFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "DONOTUSEANYTHINGINTHISFILE.h", "Source.h", "Helper.cpp", "Helper.h",
            "Source.cpp", "Test.cpp", "Test.h", "Tester.cpp", "Tester.h",
            "Utility.cpp", "Utility.h", "UI.h", "ShopUtils.cpp", "ShopUtils.h",
            "LabUI.h", "resource.h", "LLMChecker.h", "ProgressBar.h", "Result.h",
            "Results.h", "ResultsLib.h", "LabTestUtils.h", "Console.h", "Console.cpp"
        };

        private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
        {
            ".vs", "bin", "obj", "Debug", "Release", "x64", "x86"
        };

        private static readonly Regex QuotedIncludeRegex = new(
            @"^\s*#\s*include\s+""([^""]+)""",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex DeclaredTypeRegex = new(
            @"^\s*(?:template\s*<[^>]+>\s*)?(?:class|struct|enum(?:\s+class)?)\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        public static IReadOnlyList<RelatedSubmissionFile> FindRelatedFiles(
            string? primaryPath,
            IEnumerable<string>? additionalPaths = null,
            string? searchRoot = null,
            int maxFiles = 12)
        {
            var relatedPaths = new List<string>();
            if (string.IsNullOrWhiteSpace(primaryPath) || !File.Exists(primaryPath))
                return Array.Empty<RelatedSubmissionFile>();

            var primaryFullPath = Path.GetFullPath(primaryPath);
            var directory = Path.GetDirectoryName(primaryFullPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return Array.Empty<RelatedSubmissionFile>();

            var resolvedSearchRoot = ResolveSearchRoot(primaryFullPath, searchRoot);

            AddPairedSources(primaryFullPath, directory, relatedPaths);
            AddQuotedIncludes(primaryFullPath, relatedPaths, resolvedSearchRoot);

            if (additionalPaths != null)
            {
                foreach (var extra in additionalPaths)
                    if (resolvedSearchRoot != null && ReviewContext.Contains(resolvedSearchRoot, extra))
                        AddIfRelated(extra, primaryFullPath, relatedPaths);
            }

            AddSameDirectorySources(primaryFullPath, directory, relatedPaths);

            // Follow includes from discovered related files so Item.h / Inventory.h
            // are included even when they live in a nested folder.
            for (var i = 0; i < relatedPaths.Count; i++)
                AddQuotedIncludes(relatedPaths[i], relatedPaths, resolvedSearchRoot);

            var results = new List<RelatedSubmissionFile>();
            foreach (var path in relatedPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(1, maxFiles)))
            {
                try
                {
                    var content = BoundedTextReader.Read(path);
                    results.Add(new RelatedSubmissionFile
                    {
                        FileName = Path.GetFileName(path),
                        FilePath = path,
                        Extension = Path.GetExtension(path),
                        Content = content,
                        DeclaredTypes = ExtractDeclaredTypes(content, Path.GetFileNameWithoutExtension(path))
                    });
                }
                catch
                {
                    // Skip unreadable files rather than failing grading.
                }
            }

            return results;
        }

        public static IReadOnlyList<string> ExtractDeclaredTypes(string? content, string? fileStem = null)
        {
            var types = new List<string>();
            if (!string.IsNullOrWhiteSpace(fileStem) &&
                Regex.IsMatch(fileStem, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                types.Add(fileStem);
            }

            if (!string.IsNullOrEmpty(content))
            {
                foreach (Match match in DeclaredTypeRegex.Matches(content))
                {
                    var typeName = match.Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(typeName))
                        types.Add(typeName);
                }
            }

            return types
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static void AddPairedSources(string primaryPath, string directory, List<string> relatedPaths)
        {
            var stem = Path.GetFileNameWithoutExtension(primaryPath);
            foreach (var extension in SourceExtensions)
                AddIfRelated(Path.Combine(directory, stem + extension), primaryPath, relatedPaths);
        }

        private static void AddQuotedIncludes(string sourcePath, List<string> relatedPaths, string? searchRoot)
        {
            string text;
            try
            {
                text = BoundedTextReader.Read(sourcePath);
            }
            catch
            {
                return;
            }

            var directory = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(directory))
                return;

            foreach (Match match in QuotedIncludeRegex.Matches(text))
            {
                var includePath = match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
                var directInclude = Path.GetFullPath(Path.Combine(directory, includePath));
                if (searchRoot != null && ReviewContext.Contains(searchRoot, directInclude))
                    AddIfRelated(directInclude, sourcePath, relatedPaths);

                var searchDirectory = !string.IsNullOrWhiteSpace(searchRoot) && Directory.Exists(searchRoot)
                    ? searchRoot
                    : directory;

                try
                {
                    var matches = Directory.GetFiles(searchDirectory, Path.GetFileName(includePath), SearchOption.AllDirectories)
                        .Where(path => !IsInExcludedDirectory(path));
                    foreach (var matchPath in matches)
                        AddIfRelated(matchPath, sourcePath, relatedPaths);
                }
                catch
                {
                    // Ignore search failures and keep grading.
                }
            }
        }

        private static void AddSameDirectorySources(string primaryPath, string directory, List<string> relatedPaths)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(directory);
            }
            catch
            {
                return;
            }

            foreach (var file in files)
                AddIfRelated(file, primaryPath, relatedPaths);
        }

        private static string? ResolveSearchRoot(string primaryPath, string? searchRoot)
        {
            if (string.IsNullOrWhiteSpace(searchRoot) || !Directory.Exists(searchRoot))
                return Path.GetDirectoryName(primaryPath);

            try
            {
                var fullRoot = Path.GetFullPath(searchRoot);
                var fullPrimary = Path.GetFullPath(primaryPath);
                if (ReviewContext.Contains(fullRoot, fullPrimary))
                    return fullRoot;
            }
            catch
            {
            }

            return Path.GetDirectoryName(primaryPath);
        }

        private static bool IsInExcludedDirectory(string path)
        {
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Any(part => ExcludedDirectories.Contains(part));
        }

        private static void AddIfRelated(string? candidatePath, string primaryPath, List<string> relatedPaths)
        {
            if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath))
                return;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidatePath);
            }
            catch
            {
                return;
            }

            if (string.Equals(fullPath, Path.GetFullPath(primaryPath), StringComparison.OrdinalIgnoreCase))
                return;

            var fileName = Path.GetFileName(fullPath);
            if (ExcludedFileNames.Contains(fileName))
                return;

            if (!SourceExtensions.Contains(Path.GetExtension(fullPath)))
                return;

            if (!relatedPaths.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                relatedPaths.Add(fullPath);
        }
    }
}
