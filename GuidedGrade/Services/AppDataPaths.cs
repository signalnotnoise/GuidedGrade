using System.Diagnostics;
using System.IO;

namespace GuidedGrade.Services;

internal static class AppDataPaths
{
    internal const string ProductDirectoryName = "GuidedGrade";
    internal const string LegacyProductDirectoryName = "LabFeedbackWPF";

    private static readonly object MigrationLock = new();

    internal static string RoamingDirectory =>
        EnsureProductDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    internal static string LocalDirectory =>
        EnsureProductDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    internal static string MigrateLegacyLocalPath(string path) =>
        MigrateLegacyPath(path, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    internal static string MigrateLegacyPath(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            return path;

        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var legacy = Path.GetFullPath(Path.Combine(root, LegacyProductDirectoryName));
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(legacy, fullPath);
        if (Path.IsPathRooted(relative) ||
            relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return path;
        }

        var current = EnsureProductDirectory(root);
        var migrated = relative.Equals(".", StringComparison.Ordinal)
            ? current
            : Path.Combine(current, relative);
        return File.Exists(migrated) || Directory.Exists(migrated) ? migrated : path;
    }

    internal static string EnsureProductDirectory(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        lock (MigrationLock)
        {
            var current = Path.Combine(root, ProductDirectoryName);
            var legacy = Path.Combine(root, LegacyProductDirectoryName);

            if (!Directory.Exists(current) && Directory.Exists(legacy))
            {
                Directory.Move(legacy, current);
                return current;
            }

            Directory.CreateDirectory(current);
            if (Directory.Exists(legacy))
                MergeLegacyDirectory(legacy, current);

            return current;
        }
    }

    private static void MergeLegacyDirectory(string legacy, string current)
    {
        foreach (var sourceDirectory in Directory.EnumerateDirectories(legacy))
        {
            if ((File.GetAttributes(sourceDirectory) & FileAttributes.ReparsePoint) != 0)
            {
                Trace.WriteLine($"Legacy GuidedGrade migration skipped reparse point: {sourceDirectory}");
                continue;
            }

            var destinationDirectory = Path.Combine(current, Path.GetFileName(sourceDirectory));
            Directory.CreateDirectory(destinationDirectory);
            MergeLegacyDirectory(sourceDirectory, destinationDirectory);
        }

        foreach (var sourceFile in Directory.EnumerateFiles(legacy))
        {
            var destinationFile = Path.Combine(current, Path.GetFileName(sourceFile));
            if (File.Exists(destinationFile))
            {
                Trace.WriteLine($"Legacy GuidedGrade migration retained conflicting file: {sourceFile}");
                continue;
            }

            File.Move(sourceFile, destinationFile);
        }

        if (Directory.EnumerateFileSystemEntries(legacy).Any())
            return;

        try
        {
            Directory.Delete(legacy);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"Legacy GuidedGrade migration could not remove empty directory '{legacy}': {error.Message}");
        }
    }
}
