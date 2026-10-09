using GuidedGrade.Models;

namespace GuidedGrade.Services;

// Owns review records and their bounded cache; has no window or dispatcher dependency.
internal sealed class ReviewWorkspaceService
{
    private readonly CommentPersistenceService _store;
    internal BoundedCache<List<SectionFeedback>> Comments { get; }

    internal ReviewWorkspaceService(CommentPersistenceService store, Func<string?> activeFile)
    {
        _store = store;
        Comments = new(128, activeFile);
    }

    internal List<SectionFeedback> Reload(string path) => Comments[path] = _store.LoadComments(path);

    internal void Track(string path, SectionFeedback feedback)
    {
        if (!Comments.TryGetValue(path, out var comments)) comments = Reload(path);
        var index = comments.FindIndex(existing =>
            ReviewContext.Matches(existing.ReviewContext, feedback.ReviewContext) &&
            existing.IsOverallReview == feedback.IsOverallReview &&
            existing.RubricReview?.CriterionId == feedback.RubricReview?.CriterionId &&
            existing.RubricReview?.IsDeduction == feedback.RubricReview?.IsDeduction &&
            string.Equals(existing.SectionName, feedback.SectionName, StringComparison.OrdinalIgnoreCase) &&
            existing.StartLine == feedback.StartLine && existing.EndLine == feedback.EndLine);
        if (index >= 0) comments[index] = feedback;
        else comments.Add(feedback);
    }

    internal bool SaveCached(string path)
    {
        // Cache eviction must never erase records that were not loaded.
        if (!Comments.TryGetValue(path, out var comments)) return false;
        _store.SaveComments(path, comments);
        Reload(path);
        return true;
    }

    internal StudentGrade? CalculateGrade(string context)
    {
        var comments = _store.GetAllReviewedPaths().SelectMany(_store.LoadComments)
            .Where(comment => ReviewContext.Matches(comment.ReviewContext, context)).ToArray();
        var possible = comments.Where(comment => comment.RubricReview is { IsDeduction: false })
            .OrderByDescending(comment => comment.RubricReview!.ReportId, StringComparer.Ordinal)
            .GroupBy(comment => comment.RubricReview!.CriterionId)
            .Sum(group => group.First().RubricReview!.MaximumPoints);
        return possible > 0 ? new(Math.Max(0, InlineRubricReview.ApprovedPoints(comments)), possible) : null;
    }

    internal sealed record Completion(string[] Paths, SectionFeedback[] Removed, bool UpdateGrade);

    internal Completion CompleteInline((string Path, long Version)[] targets, string context,
        IReadOnlyList<ReviewFindingEnvelope.FindingDraft> findings, bool autoApprove, ReviewGeneration generations)
    {
        if (targets.Length == 0 || targets.Any(target => !generations.IsCurrent(target.Path, target.Version)))
            return new([], [], false);
        var sources = targets.Select(target => BoundedTextReader.Read(target.Path)).ToArray();
        var existing = targets.ToDictionary(target => target.Path, target => _store.LoadComments(target.Path), StringComparer.OrdinalIgnoreCase);
        var pinned = existing.Values.SelectMany(comments => comments)
            .Where(comment => ReviewContext.Matches(comment.ReviewContext, context) && comment.IsPinned).ToArray();
        var reportId = $"{DateTime.UtcNow.Ticks:D19}-{Guid.NewGuid():N}";
        var routed = findings.Where(finding => !pinned.Any(comment => finding.Id == 0
            ? comment.IsOverallReview : comment.RubricReview?.CriterionId == finding.Id && comment.RubricReview.IsDeduction == finding.IsDeduction))
            .Select(finding =>
            {
                var location = InlineRubricReview.Locate(finding, sources);
                return (Path: targets[location.File - 1].Path, Comment: InlineRubricReview.Create(finding, location.Line, context, reportId, autoApprove));
            }).ToArray();
        var removed = new List<SectionFeedback>();
        foreach (var target in targets)
        {
            var comments = existing[target.Path];
            var old = comments.Where(comment => ReviewContext.Matches(comment.ReviewContext, context) &&
                !comment.IsPinned && (comment.IsOverallReview || comment.RubricReview != null)).ToArray();
            removed.AddRange(old);
            comments.RemoveAll(old.Contains);
            comments.AddRange(routed.Where(row => string.Equals(row.Path, target.Path, StringComparison.OrdinalIgnoreCase)).Select(row => row.Comment));
            _store.SaveComments(target.Path, comments);
            Reload(target.Path);
        }
        return new(targets.Select(target => target.Path).ToArray(), removed.ToArray(),
            routed.Any(row => row.Comment.ReviewStatus == FeedbackReviewStatus.Approved) || pinned.Any(comment => comment.ReviewStatus == FeedbackReviewStatus.Approved));
    }
}
