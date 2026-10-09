using System.Text.RegularExpressions;
using GuidedGrade.Models;
namespace GuidedGrade.Services;
internal static class InlineRubricReview
{
    internal static (int File, int Line) Locate(ReviewFindingEnvelope.FindingDraft finding, IReadOnlyList<string> sources)
    {
        if (finding.Id == 0) return (1, 1);
        var order = Enumerable.Range(0, sources.Count).OrderBy(index => index + 1 == finding.File ? 0 : 1);
        foreach (var index in order)
        {
            if (string.IsNullOrWhiteSpace(finding.Quote)) break;
            var offset = sources[index].IndexOf(finding.Quote, StringComparison.Ordinal);
            if (offset < 0 && finding.Quote.Length <= 4096)
            {
                var pattern = string.Join(@"\s+", finding.Quote.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape));
                var match = Regex.Match(sources[index], pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                if (match.Success) offset = match.Index;
            }
            if (offset >= 0) return (index + 1, 1 + sources[index][..offset].Count(c => c == '\n'));
        }
        // Explicit TODO section labels are reliable local anchors even when the model quote is invalid.
        var section = Regex.Match(finding.Name, @"\b[A-C][- ]?[1-9]\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        if (section.Success)
        {
            var label = Regex.Replace(section.Value, "[- ]", "");
            foreach (var index in order)
            {
                var lines = sources[index].Split('\n');
                for (var line = 0; line < lines.Length; line++)
                    if (lines[line].Contains("TODO", StringComparison.OrdinalIgnoreCase) &&
                        Regex.IsMatch(lines[line], @"\b" + label[0] + @"[- ]?" + label[1] + @"\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                        return (index + 1, line + 1);
            }
        }
        return (finding.File >= 1 && finding.File <= sources.Count ? finding.File : 1, 0);
    }
    internal static SectionFeedback Create(ReviewFindingEnvelope.FindingDraft finding, int line, string context, string reportId, bool autoApprove)
        => new()
        {
            IsOverallReview = finding.Id == 0, SectionName = finding.Name, Explanation = finding.Reason, SuggestedScore = finding.Earned ?? 0,
            StartLine = Math.Max(1, line), EndLine = Math.Max(1, line), ReviewContext = context,
            Issues = finding.Verified || finding.Id == 0 ? [] : ["Model finding is unverified. Review the code, then approve, modify or reject."],
            RubricReview = finding.Id == 0 ? null : new() { CriterionId = finding.Id, IsDeduction = finding.IsDeduction, MaximumPoints = finding.Maximum, EvidenceVerified = finding.Verified,
                LocationResolved = line > 0, ReportId = reportId },
            ReviewStatus = autoApprove && finding.Verified && line > 0 && !finding.RequiresInstructorConfirmation ? FeedbackReviewStatus.Approved : FeedbackReviewStatus.Pending
        };
    internal static double ApprovedPoints(IEnumerable<SectionFeedback> comments) => comments
        .Where(c => c.RubricReview != null).OrderByDescending(c => c.RubricReview!.ReportId, StringComparer.Ordinal).GroupBy(c => (c.RubricReview!.IsDeduction, c.RubricReview.CriterionId))
        .Sum(group => { var row = group.First(); return row.ReviewStatus == FeedbackReviewStatus.Approved ? (row.RubricReview!.IsDeduction ? -row.SuggestedScore : row.SuggestedScore) : 0; });
}
