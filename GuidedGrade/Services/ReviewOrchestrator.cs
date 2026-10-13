using System.IO;
using GuidedGrade.Models;
namespace GuidedGrade.Services;
internal static class ReviewOrchestrator
{
    internal static async Task<string> ReviewAsync(GradingAssignment? assignment, LLMSettings settings,
        IEnumerable<string> paths, IEnumerable<string> identifiers, CancellationToken token = default,
        Func<string, System.Text.Json.JsonElement?, CancellationToken, Task<string>>? complete = null)
    {
        var names = identifiers.ToArray();
        var files = new List<OllamaService.CodeFile>();
        var totalBytes = 0L;
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (files.Count >= 32) throw new IOException("Select at most 32 files for one review.");
            var content = await BoundedTextReader.ReadAsync(path, token);
            totalBytes += System.Text.Encoding.UTF8.GetByteCount(content);
            if (totalBytes > 8 * 1024 * 1024) throw new IOException("Selected files exceed the 8 MB review limit. No partial submission was sent.");
            files.Add(new() { Name = StudentDataSanitizer.AnonymousFileName(files.Count + 1, Path.GetExtension(path)),
                Content = StudentDataSanitizer.Sanitize(content, names) });
        }
        if (files.Count == 0) throw new InvalidOperationException("Select at least one source file.");
        var prompt = OverallFeedbackPrompt.WithFiles(
            OverallFeedbackPrompt.BuildInstructions(assignment, settings.RequirementsTemplate, names), files);
        return await OverallReviewService.AnalyzeAsync(assignment, prompt, files,
            complete ?? ((payload, schema, ct) => LlmCompletionService.CompleteAsync(settings, OverallReviewService.SystemPrompt,
                payload, ct, schema, LlmJobPriority.Assignment, "Submission review")), token);
    }
}
