using System.IO;
using GuidedGrade.Models;
namespace GuidedGrade.Services;
internal static class SubmissionFolderLoader
{
    internal sealed record StudentsResult(List<Student> Students, IReadOnlyList<string> Warnings);
    internal static StudentsResult LoadStudents(string root, bool useFolderNames)
    {
        var students = new List<Student>();
        var warnings = new List<string>();
        try
        {
            foreach (var path in Directory.EnumerateDirectories(root))
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
                if (students.Count >= 5000) { warnings.Add("Submission folder limit reached (5,000)."); break; }
                try { students.Add(useFolderNames ? new Student(Path.GetFileName(path), "", "", path) : new Student(root, path)); }
                catch (ArgumentException) { if (warnings.Count < 20) warnings.Add($"Skipped folder with an invalid student name: {Path.GetFileName(path)}"); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { warnings.Add($"Could not load submission folders: {ex.Message}"); }
        return new(students, warnings);
    }
    internal static IReadOnlyList<string> Populate(FileSystemItem parent, string root, int maxItems = 5000, int maxDepth = 32)
    {
        var warnings = new List<string>();
        var count = 0;
        void Load(FileSystemItem node, string directory, int depth)
        {
            if (depth >= maxDepth) { warnings.Add($"Folder depth limit reached: {directory}"); return; }
            try
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(directory).OrderBy(Path.GetFileName))
                {
                    if (++count > maxItems) { if (warnings.Count == 0 || !warnings[^1].StartsWith("Folder item limit")) warnings.Add("Folder item limit reached; some files are not displayed."); return; }
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    var child = new FileSystemItem(path, (attributes & FileAttributes.Directory) != 0);
                    node.Children.Add(child);
                    if (child.IsDirectory) Load(child, path, depth + 1);
                    if (count > maxItems) return;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { warnings.Add($"Could not load {directory}: {ex.Message}"); }
        }
        Load(parent, root, 0);
        return warnings;
    }
}
