using System.IO;
using System.Text.RegularExpressions;
using Lab_Feedback_WPF.Models;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>
    /// Removes student-identifying information before any text is sent to an LLM.
    /// </summary>
    public static class StudentDataSanitizer
    {
        private static readonly Regex EmailRegex = new(
            @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex WindowsUserPathRegex = new(
            @"[A-Za-z]:\\Users\\[^\\\s]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex UnixUserPathRegex = new(
            @"/home/[^/\s]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string AnonymousFileName(int index = 1, string? extension = null)
        {
            var ext = string.IsNullOrWhiteSpace(extension) ? ".cpp" : extension;
            if (!ext.StartsWith('.'))
                ext = "." + ext;
            return $"section-{Math.Max(1, index)}{ext}";
        }

        public static string Sanitize(string? text, IEnumerable<string?>? identifiers = null)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sanitized = text;

            if (identifiers != null)
            {
                foreach (var identifier in identifiers
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id!.Trim())
                    .Where(id => id.Length >= 2)
                    .OrderByDescending(id => id.Length)
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    sanitized = Regex.Replace(
                        sanitized,
                        Regex.Escape(identifier),
                        "[REDACTED]",
                        RegexOptions.IgnoreCase);
                }
            }

            sanitized = EmailRegex.Replace(sanitized, "[REDACTED_EMAIL]");
            sanitized = WindowsUserPathRegex.Replace(sanitized, @"C:\Users\[REDACTED]");
            sanitized = UnixUserPathRegex.Replace(sanitized, "/home/[REDACTED]");
            return sanitized;
        }

        public static IReadOnlyList<string> GetIdentifiers(Student? student, string? filePath = null, string? openedDirectoryPath = null)
        {
            var identifiers = new List<string>();

            void Add(string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    identifiers.Add(value.Trim());
            }

            if (student != null)
            {
                Add(student.FirstName);
                Add(student.LastName);
                Add(student.IdNumber);
                Add(student.FullName);
                Add(student.Folder);
                if (!string.IsNullOrWhiteSpace(student.Folder))
                    Add(Path.GetFileName(student.Folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            }

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                Add(filePath);
                Add(Path.GetDirectoryName(filePath));

                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (!string.IsNullOrWhiteSpace(fileName) &&
                    identifiers.Any(id => fileName.Contains(id, StringComparison.OrdinalIgnoreCase)))
                {
                    Add(fileName);
                    Add(Path.GetFileName(filePath));
                }
            }

            if (!string.IsNullOrWhiteSpace(openedDirectoryPath))
            {
                Add(openedDirectoryPath);
                Add(Path.GetFileName(openedDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            }

            return identifiers;
        }
    }
}
