using GuidedGrade.Models;
using GuidedGrade.Services;
using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class ReviewRubricViewModel(GradingAssignment? assignment, Action edit)
{
 internal GradingAssignment? Assignment { get; } = assignment == null ? null : ReviewContext.Snapshot(assignment);
 internal State<bool> ShowInstructions { get; } = new(false);
 internal Action Edit { get; } = edit;
}
