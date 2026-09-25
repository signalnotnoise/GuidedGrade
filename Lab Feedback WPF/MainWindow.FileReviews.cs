using Lab_Feedback_WPF.Models;

namespace Lab_Feedback_WPF;

public partial class MainWindow
{
    internal (string Path, long Version)[] CaptureOverallReviewFiles(IEnumerable<string> paths) =>
        paths.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (path, _reviewGeneration.Capture(path))).ToArray();

    internal void CompleteOverallFileReview((string Path, long Version)[] targets, string draftTarget, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var saved = false;
        foreach (var target in targets)
        {
            if (!_reviewGeneration.IsCurrent(target.Path, target.Version)) continue;
            var review = new SectionFeedback
            {
                IsOverallReview = true,
                SectionName = targets.Length == 1 ? "Overall file review" : $"Overall review of {targets.Length} checked files",
                StartLine = 1, EndLine = 1,
                Explanation = targets.Length == 1 ? text : "Combined feedback for the checked files; this is not an individual file score.\n\n" + text
            };
            // A checked file need not have been opened. Preserve its existing saved section reviews.
            var comments = _fileComments.TryGetValue(target.Path, out var cached)
                ? cached.ToList() : _commentPersistenceService.LoadComments(target.Path);
            comments.RemoveAll(comment => comment.IsOverallReview);
            comments.Add(review);
            _commentPersistenceService.SaveComments(target.Path, comments);
            _fileComments[target.Path] = comments;
            MarkFeedbackImported(target.Path, review, draftTarget);
            RenderCommentsForFile(target.Path);
            saved = true;
        }
        if (saved) PresentGeneratedFeedback(draftTarget, text);
    }
}
