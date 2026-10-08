using GuidedGrade.Models;
using GuidedGrade.Presentation;
using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class DeductionEditorViewModel(AssignmentDeduction item)
{
    internal RubricEditorRow Row { get; } = new(item.Rule, item.Points);
    internal State<bool> Confirm { get; } = new(item.RequiresInstructorConfirmation);
}
internal sealed class AssignmentDeductionsViewModel
{
    internal StateList<DeductionEditorViewModel> Rows { get; } = new();
    internal void Load(IEnumerable<AssignmentDeduction> items) { Rows.Clear(); foreach (var item in items) Rows.Add(new(item)); }
    internal List<AssignmentDeduction> Build() => Rows.Select(row => row.Row.TryPoints(out var points) && !string.IsNullOrWhiteSpace(row.Row.Name.Value)
        ? new AssignmentDeduction { Rule = row.Row.Name.Value.Trim(), Points = points, RequiresInstructorConfirmation = row.Confirm.Value }
        : throw new ArgumentException("Every deduction needs a rule and positive penalty points.")).ToList();
}
