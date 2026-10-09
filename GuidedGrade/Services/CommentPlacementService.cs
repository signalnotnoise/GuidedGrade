using GuidedGrade.Models;
namespace GuidedGrade.Services;
internal static class CommentPlacementService
{
    internal static bool Move(SectionFeedback feedback, int line, int lineCount)
    {
        if (feedback.IsPinned || line < 1 || line > lineCount) return false;
        var span = Math.Max(0, feedback.EndLine - feedback.StartLine);
        feedback.StartLine = line; feedback.EndLine = Math.Min(lineCount, line + span);
        if (feedback.RubricReview != null) feedback.RubricReview.LocationResolved = true;
        return true;
    }
    internal static void TogglePin(SectionFeedback feedback)
    {
        feedback.IsPinned = !feedback.IsPinned;
        if (feedback.IsPinned && feedback.RubricReview != null) feedback.RubricReview.LocationResolved = true;
    }
}
