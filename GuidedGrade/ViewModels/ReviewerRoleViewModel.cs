using GuidedGrade.Models;
using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class ReviewerRoleViewModel
{
    internal State<string> Text { get; } = new(AssignmentFeedbackOptions.DefaultReviewerRole);
    internal void Reset() => Text.Value = AssignmentFeedbackOptions.DefaultReviewerRole;
    internal string Build() => string.IsNullOrWhiteSpace(Text.Value) ? AssignmentFeedbackOptions.DefaultReviewerRole : Text.Value.Trim();
}
