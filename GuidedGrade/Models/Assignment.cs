using System.IO;
using System.Text.Json;

namespace GuidedGrade.Models
{
    public class Assignment
    {
        public string? Name { get; }
        public string? Folder { get; }

        public Assignment(string? name, string? folder)
        {
            Name = name;
            Folder = folder;
        }

        public static List<Assignment> FindLabOrPracticalSubfolders(string folder)
        {
            var assignments = new List<Assignment>();
            SearchSubfolders(folder, assignments);

            return assignments
                .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(a => ParseSubmissionDate(a.Folder)).First())
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static void ConsolidateSubmissions(string studentFolder)
        {
            if (!Directory.Exists(studentFolder)) return;

            var submissionFolders = Directory.GetDirectories(studentFolder)
                .Where(d => Path.GetFileName(d)
                    .StartsWith("submission_", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!submissionFolders.Any()) return;

            var submissionContents = new List<(string SubmissionFolder, DateTime Date, List<string> LabFolders)>();

            foreach (var submission in submissionFolders)
            {
                var date = ParseSubmissionDate(submission);
                var labFolders = Directory.GetDirectories(submission, "*", SearchOption.AllDirectories)
                    .Where(d =>
                    {
                        var name = Path.GetFileName(d);

                        if (!name.StartsWith("Lab ") && !name.Contains("Practical"))
                            return false;

                        var parts = d.Split(Path.DirectorySeparatorChar);
                        if (parts.Any(p => p.StartsWith("."))) return false;

                        if (name.EndsWith(".tlog")) return false;

                        return true;
                    })
                    .ToList();

                submissionContents.Add((submission, date, labFolders));
            }

            var latestPerLab = submissionContents
                .SelectMany(s => s.LabFolders.Select(l => (s.Date, LabFolder: l)))
                .GroupBy(x => Path.GetFileName(x.LabFolder), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.Date).First().LabFolder)
                .ToList();

            foreach (var labFolder in latestPerLab)
            {
                var labName = Path.GetFileName(labFolder);
                var destination = Path.Combine(studentFolder, labName);

                try
                {
                    if (Directory.Exists(destination))
                        Directory.Delete(destination, recursive: true);

                    Directory.Move(labFolder, destination);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error moving {labName}: {ex.Message}");
                }
            }

            foreach (var (submissionFolder, _, _) in submissionContents)
            {
                try
                {
                    Directory.Delete(submissionFolder, recursive: true);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error deleting {Path.GetFileName(submissionFolder)}: {ex.Message}");
                }
            }
        }

        private static DateTime ParseSubmissionDate(string? folderPath)
        {
            if (folderPath == null) return DateTime.MinValue;

            var parts = folderPath.Split(Path.DirectorySeparatorChar);
            foreach (var part in parts)
            {
                if (!part.StartsWith("submission_", StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = System.Text.RegularExpressions.Regex.Match(
                    part, @"(\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})");

                if (match.Success &&
                    DateTime.TryParseExact(
                        match.Value,
                        "yyyy-MM-dd_HH-mm-ss",
                        null,
                        System.Globalization.DateTimeStyles.None,
                        out var date))
                {
                    return date;
                }
            }

            return DateTime.MinValue;
        }

        private static void SearchSubfolders(string directoryPath, List<Assignment> assignments)
        {
            if (!Directory.Exists(directoryPath)) return;

            foreach (var entry in Directory.GetDirectories(directoryPath))
            {
                var subfolderName = Path.GetFileName(entry);

                if (subfolderName.StartsWith(".") || subfolderName == "bin" || subfolderName == "obj")
                    continue;

                if (subfolderName.StartsWith("Lab ") || subfolderName.Contains("Practical"))
                    assignments.Add(new Assignment(subfolderName, entry));
                else
                    SearchSubfolders(entry, assignments);
            }
        }

        private static bool IsSubmissionFolder(string folderName) =>
            folderName.StartsWith("submission_", StringComparison.OrdinalIgnoreCase);
    }
}