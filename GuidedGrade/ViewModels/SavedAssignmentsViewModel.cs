using GuidedGrade.Models;
using GuidedGrade.Services;
using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class SavedAssignmentsViewModel
{
    internal StateList<GradingAssignment> Items { get; } = new();
    internal State<int> Index { get; } = new(-1);
    internal GradingAssignment? Selected => Index.Value >= 0 && Index.Value < Items.Count ? Items[Index.Value] : null;
    internal SavedAssignmentsViewModel(AssignmentPersistenceService persistence) { foreach (var item in persistence.GetAllAssignments()) Items.Add(item); }
}
