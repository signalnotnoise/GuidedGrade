using GuidedGrade.Models;

namespace GuidedGrade;

public partial class MainWindow
{
    private void EnsureSingleReviewContext(IEnumerable<string> paths, string target)
    {
        if (paths.Any(path => CurrentFeedbackKey(path) != target))
            throw new InvalidOperationException("The checked files span multiple lab folders. Check files from one lab at a time before queuing a review.");
    }

    internal (string Path, long Version)[] CaptureOverallReviewFiles(IEnumerable<string> paths) =>
        paths.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (path, _reviewGeneration.Capture(path))).ToArray();

    internal void CompleteOverallFileReview((string Path, long Version)[] targets, string draftTarget, string text,
        bool approve = false, bool publishToDraft = true)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var display = Services.ReviewWarningEnvelope.Unpack(text);
        text = display.Text;
        if (display.Warnings.Count > 0) { approve = false; publishToDraft = false; }
        var saved = false;
        foreach (var target in targets)
        {
            if (!_reviewGeneration.IsCurrent(target.Path, target.Version)) continue;
            var review = new SectionFeedback
            {
                IsOverallReview = true,
                Issues = display.Warnings.ToList(),
                ReviewStatus = approve ? FeedbackReviewStatus.Approved : FeedbackReviewStatus.Pending,
                ReviewContext = draftTarget,
                SectionName = targets.Length == 1 ? "Overall file review" : $"Overall review of {targets.Length} checked files",
                StartLine = 1, EndLine = 1,
                Explanation = targets.Length == 1 ? text : "Combined feedback for the checked files; this is not an individual file score.\n\n" + text
            };
            // A checked file need not have been opened. Preserve its existing saved section reviews.
            var comments = _fileComments.TryGetValue(target.Path, out var cached)
                ? cached.ToList() : _commentPersistenceService.LoadComments(target.Path);
            var old = comments.Where(comment => comment.IsOverallReview && comment.ReviewContext == draftTarget).ToArray();
            var cleanup = old.SelectMany(comment => new[] { comment, new SectionFeedback { IsOverallReview = true, Explanation = comment.Explanation.Replace("Combined feedback for the checked files; this is not an individual file score.\n\n", "", StringComparison.Ordinal) } });
            if (_reviewDrafts.TryGetValue(draftTarget, out var previous)) _reviewDrafts[draftTarget] = Services.ReviewDraftCleanup.Remove(previous, cleanup);
            comments.RemoveAll(comment => comment.IsOverallReview && comment.ReviewContext == draftTarget);
            comments.Add(review);
            _commentPersistenceService.SaveComments(target.Path, comments);
            RefreshPersistedComments(target.Path);
            if (publishToDraft) MarkFeedbackImported(target.Path, review, draftTarget);
            RenderCommentsForFile(target.Path);
            saved = true;
        }
        if (saved) _savedReviewRevision.Value++;
        if (saved && publishToDraft) PresentGeneratedFeedback(draftTarget, text);
    }
}
