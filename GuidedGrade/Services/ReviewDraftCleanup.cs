using GuidedGrade.Models;

namespace GuidedGrade.Services;

internal static class ReviewDraftCleanup
{
    // Remove intact generated blocks only; instructor-edited text is preserved.
    internal static string Remove(string draft, IEnumerable<SectionFeedback> comments)
    {
        const string separator = "\n\n---\n\n";
        var padded = separator + draft + separator;
        foreach (var comment in comments)
        {
            foreach (var text in new[] { SavedFeedbackText.Format(comment), comment.IsOverallReview ? comment.Explanation : "" })
            {
                if (string.IsNullOrWhiteSpace(text)) continue;
                var block = separator + text + separator;
                while (padded.Contains(block, StringComparison.Ordinal)) padded = padded.Replace(block, separator, StringComparison.Ordinal);
            }
        }
        return padded.Length <= separator.Length * 2 ? "" : padded[separator.Length..^separator.Length];
    }
}
