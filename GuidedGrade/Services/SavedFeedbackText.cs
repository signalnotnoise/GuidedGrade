using GuidedGrade.Models;

namespace GuidedGrade.Services;

internal static class SavedFeedbackText
{
    public static string Format(SectionFeedback feedback) =>
        feedback.IsOverallReview ? "## " + feedback.SectionName + "\n\n" + feedback.Explanation :
        "## " + feedback.SectionName + "\n\n" + feedback.Explanation
        + "\n\nStrengths:\n" + string.Join("\n", feedback.Strengths.Select(x => "- " + x))
        + "\n\nAreas to improve:\n" + string.Join("\n", feedback.Issues.Select(x => "- " + x))
        + (string.IsNullOrWhiteSpace(feedback.SuggestedCode) ? "" : "\n\nSuggested code:\n```\n" + feedback.SuggestedCode + "\n```");
}
