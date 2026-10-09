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
        var packet = Services.ReviewFindingEnvelope.Unpack(text);
        var display = Services.ReviewWarningEnvelope.Unpack(packet.Text);
        text = display.Text;
        if (display.Warnings.Count > 0) { approve = false; publishToDraft = false; }
        if (packet.Findings.Count > 0)
        {
            CompleteInlineOverallReview(targets, draftTarget, packet.Findings, approve);
            return;
        }
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
            if (comments.Any(comment => comment.IsOverallReview && comment.ReviewContext == draftTarget && comment.IsPinned)) continue;
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
    private void CompleteInlineOverallReview((string Path, long Version)[] targets, string context,
        IReadOnlyList<Services.ReviewFindingEnvelope.FindingDraft> findings, bool autoApprove)
    {
        if (targets.Any(target => !_reviewGeneration.IsCurrent(target.Path, target.Version))) return;
        var sources = targets.Select(target => Services.BoundedTextReader.Read(target.Path)).ToArray();
        var pinned = targets.SelectMany(target => _commentPersistenceService.LoadComments(target.Path))
            .Where(comment => comment.ReviewContext == context && comment.IsPinned).ToArray();
        var reportId = $"{DateTime.UtcNow.Ticks:D19}-{Guid.NewGuid():N}";
        var routed = findings.Where(finding => !pinned.Any(comment => finding.Id == 0
            ? comment.IsOverallReview : comment.RubricReview?.CriterionId == finding.Id && comment.RubricReview.IsDeduction == finding.IsDeduction)).Select(finding =>
        {
            var location = Services.InlineRubricReview.Locate(finding, sources);
            return (Path: targets[location.File - 1].Path, Comment: Services.InlineRubricReview.Create(finding, location.Line, context, reportId, autoApprove));
        }).ToArray();
        foreach (var target in targets)
        {
            var comments = _commentPersistenceService.LoadComments(target.Path);
            var old = comments.Where(c => c.ReviewContext == context && !c.IsPinned && (c.IsOverallReview || c.RubricReview != null)).ToArray();
            if (_reviewDrafts.TryGetValue(context, out var draft)) _reviewDrafts[context] = Services.ReviewDraftCleanup.Remove(draft, old);
            comments.RemoveAll(c => old.Contains(c));
            comments.AddRange(routed.Where(row => row.Path == target.Path).Select(row => row.Comment));
            _commentPersistenceService.SaveComments(target.Path, comments);
            RefreshPersistedComments(target.Path);
            RenderCommentsForFile(target.Path);
            if (context == CurrentFeedbackKey()) RestoreApprovedFeedback(target.Path, comments);
        }
        _savedReviewRevision.Value++;
        if (routed.Any(row => row.Comment.ReviewStatus == FeedbackReviewStatus.Approved) || pinned.Any(comment => comment.ReviewStatus == FeedbackReviewStatus.Approved)) RefreshRubricGrade(context);
    }
    private void RefreshRubricGrade(string context)
    {
        var paths = _commentPersistenceService.GetAllReviewedPaths();
        var comments = paths.SelectMany(path => _commentPersistenceService.LoadComments(path)).Where(c => c.ReviewContext == context).ToArray();
        var possible = comments.Where(c => c.RubricReview is { IsDeduction: false })
            .OrderByDescending(c => c.RubricReview!.ReportId, StringComparer.Ordinal).GroupBy(c => c.RubricReview!.CriterionId).Sum(group => group.First().RubricReview!.MaximumPoints);
        if (possible > 0)
            SaveStudentGrade(context, new(Math.Max(0, Services.InlineRubricReview.ApprovedPoints(comments)), possible));
    }

}
