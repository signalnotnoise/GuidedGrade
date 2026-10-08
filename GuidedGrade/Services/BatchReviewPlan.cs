using System.IO;
using GuidedGrade.Models;

namespace GuidedGrade.Services;

internal sealed record BatchReviewItem(Student Student, string? File, string? EntryPoint, string Context, string? SkipReason)
{
    internal IReadOnlyList<string> Files { get; init; } = File == null ? [] : [File];
}

internal static class BatchReviewPlan
{
    internal static string ValidatePattern(string pattern)
    {
        pattern = pattern.Trim().Replace('\\', '/');
        var suffix = pattern.StartsWith("**/", StringComparison.Ordinal) ? pattern[3..] : pattern;
        if (string.IsNullOrWhiteSpace(suffix) || Path.IsPathRooted(suffix) ||
            suffix.IndexOfAny(['*', '?', ':']) >= 0 || suffix.Split('/').Any(p => p is ".." or "." or ""))
            throw new ArgumentException("Use a relative file path, optionally starting with **/ to search extra nested folders. Other wildcards and parent-folder paths are not supported.");
        return pattern;
    }

    internal static (string Path, string? Error) Resolve(string folder, string pattern, string missing)
    {
        var recursive = pattern.StartsWith("**/", StringComparison.Ordinal);
        var suffix = recursive ? pattern[3..] : pattern;
        var expected = Path.GetFullPath(Path.Combine(folder, suffix));
        if (!recursive) return (expected, File.Exists(expected) ? null : missing);
        if (!Directory.Exists(folder)) return (expected, missing + ": " + pattern);
        try
        {
            // Do not follow junctions/symlinks into another submission or recurse through cycles.
            var matches = Directory.EnumerateFiles(folder, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false
            }).Where(path =>
            {
                var relative = Path.GetRelativePath(folder, path).Replace('\\', '/');
                return relative.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
                    relative.EndsWith("/" + suffix, StringComparison.OrdinalIgnoreCase);
            });
            string? newest = null;
            var newestTime = DateTime.MinValue;
            foreach (var path in matches)
            {
                var modified = File.GetLastWriteTimeUtc(path);
                // Equal timestamps use a stable path order, independent of enumeration order.
                if (newest == null || modified > newestTime ||
                    (modified == newestTime && StringComparer.OrdinalIgnoreCase.Compare(path, newest) < 0))
                { newest = path; newestTime = modified; }
            }
            return newest == null ? (expected, missing + ": " + pattern) : (newest, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return (expected, "Could not search submission: " + ex.Message); }
    }

    internal static IReadOnlyList<BatchReviewItem> Create(IEnumerable<Student> students, GradingAssignment assignment,
        string relativeFile, string relativeEntry, bool buildAndRun = true)
    {
        if (!buildAndRun) relativeEntry = "";
        var relativeFiles = relativeFile.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (relativeFiles.Length == 0 || relativeFiles.Any(Path.IsPathRooted) || Path.IsPathRooted(relativeEntry))
            throw new ArgumentException("Use file paths relative to each student's folder.");
        relativeFiles = relativeFiles.Select(ValidatePattern).ToArray();
        relativeEntry = string.IsNullOrWhiteSpace(relativeEntry) ? "" : ValidatePattern(relativeEntry);
        return students.Select(student =>
        {
            if (student.Folder == null) return new BatchReviewItem(student, null, null, "", "No submission folder");
            var resolved = relativeFiles.Select(path => Resolve(student.Folder, path, "Review file missing")).ToArray();
            var files = resolved.Select(r => r.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var file = files[0];
            var root = ReviewContext.SubmissionRoot(file, student.Folder);
            // Test the copy selected for review, not an older copy whose project
            // file happens to have a newer timestamp than its source files.
            var entryRoot = relativeEntry.StartsWith("**/", StringComparison.Ordinal) ? root ?? student.Folder : student.Folder;
            var entryResult = relativeEntry.Length == 0 ? (Path: file, Error: (string?)null) : Resolve(entryRoot, relativeEntry, "Test entry point missing");
            var entry = entryResult.Path;
            var resolutionError = resolved.Select(r => r.Error).FirstOrDefault(error => error != null) ?? entryResult.Error;
            if (files.Any(path => !ReviewContext.Contains(student.Folder, path)) || !ReviewContext.Contains(student.Folder, entry))
                throw new ArgumentException("Paths must stay inside each student's folder.");
            if ((resolutionError == null || (!relativeFiles.Any(p => p.StartsWith("**/")) && !relativeEntry.StartsWith("**/"))) &&
                (root == null || !ReviewContext.Contains(root, entry) || files.Any(path => ReviewContext.SubmissionRoot(path, student.Folder) != root)))
                throw new ArgumentException("The test entry point and reviewed file must belong to the same lab.");
            if (resolutionError != null)
                return new BatchReviewItem(student, file, entry, ReviewContext.Key(student.Folder, assignment, file), resolutionError) { Files = files };
            var skip = files.Any(path => !File.Exists(path)) ? "Review file missing" : !File.Exists(entry) ? "Test entry point missing" : null;
            return new BatchReviewItem(student, file, entry, ReviewContext.Key(student.Folder, assignment, file), skip) { Files = files };
        }).ToArray();
    }

    internal static async Task ProcessStudentAsync(BatchReviewItem item, bool buildAndRun,
        Func<string, long> clear, Func<CancellationToken, Task> build,
        Func<IReadOnlyList<(string Path, long Version)>, CancellationToken, Task> review, CancellationToken token)
    {
        var versions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in item.Files)
        {
            token.ThrowIfCancellationRequested();
            versions[path] = clear(path);
        }
        if (buildAndRun) { token.ThrowIfCancellationRequested(); await build(token); }
        token.ThrowIfCancellationRequested();
        await review(item.Files.Select(path => (path, versions[path])).ToArray(), token);
    }

    internal static async Task RunAsync(IReadOnlyList<BatchReviewItem> items,
        Func<BatchReviewItem, CancellationToken, Task> run, Action<BatchReviewItem, string> report, CancellationToken token)
    {
        foreach (var item in items)
        {
            token.ThrowIfCancellationRequested();
            if (item.SkipReason != null) { report(item, "Skipped: " + item.SkipReason); continue; }
            report(item, "Running");
            try
            {
                await run(item, token);
                token.ThrowIfCancellationRequested();
                report(item, "Finished");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) { report(item, "Failed: " + ex.Message); }
        }
    }
}
