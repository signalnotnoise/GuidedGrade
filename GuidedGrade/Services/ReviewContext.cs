using System.IO;
using System.Text.Json;
using GuidedGrade.Models;

namespace GuidedGrade.Services;

internal static class ReviewContext
{
    internal static bool Matches(string first, string second)
    {
        if (first == second) return true;
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second)) return false;
        try
        {
            var a = JsonSerializer.Deserialize<string?[]>(first);
            var b = JsonSerializer.Deserialize<string?[]>(second);
            return a is { Length: 4 } && b is { Length: 4 } &&
                string.Equals(a[0], b[0], StringComparison.OrdinalIgnoreCase) &&
                a[1] == b[1] && a[2] == b[2] &&
                string.Equals(a[3], b[3], StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException) { return false; }
    }

    internal static bool Contains(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path)) return false;
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return !Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    // A student's immediate child directory identifies the submitted lab; nested
    // source/header directories belong to that same submission.
    internal static string? SubmissionRoot(string? path, string? studentFolder)
    {
        if (string.IsNullOrWhiteSpace(path)) return studentFolder;
        var fullPath = Path.GetFullPath(path);
        if (!string.IsNullOrWhiteSpace(studentFolder) && Contains(studentFolder, fullPath))
        {
            var relative = Path.GetRelativePath(studentFolder, fullPath);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Length > 1 || (relative != "." && Directory.Exists(fullPath)))
                return Path.Combine(Path.GetFullPath(studentFolder), parts[0]);
            return Path.GetFullPath(studentFolder);
        }
        return Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath);
    }

    internal static string Key(string? studentFolder, GradingAssignment? assignment, string? path) =>
        JsonSerializer.Serialize(new[] { studentFolder, assignment?.Course, assignment?.Title,
            SubmissionRoot(path, studentFolder)?.ToUpperInvariant() });

    internal static GradingAssignment? Snapshot(GradingAssignment? assignment) => assignment == null ? null : new()
    {
        Course = assignment.Course, Title = assignment.Title, Requirements = assignment.Requirements,
        Rubric = assignment.Rubric.Select(item => new RubricItem(item.Name, item.MaxPoints)).ToList()
    };
}
