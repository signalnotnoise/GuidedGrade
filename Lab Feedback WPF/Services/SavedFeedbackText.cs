using Lab_Feedback_WPF.Models;

namespace Lab_Feedback_WPF.Services;

internal static class SavedFeedbackText
{
    public static string Format(SectionFeedback feedback) =>
        "## " + feedback.SectionName + "\n\n" + feedback.Explanation
        + "\n\nStrengths:\n" + string.Join("\n", feedback.Strengths.Select(x => "- " + x))
        + "\n\nAreas to improve:\n" + string.Join("\n", feedback.Issues.Select(x => "- " + x))
        + (string.IsNullOrWhiteSpace(feedback.SuggestedCode) ? "" : "\n\nSuggested code:\n```\n" + feedback.SuggestedCode + "\n```");
}
