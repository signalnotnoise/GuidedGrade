using GuidedGrade.Models;
using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class AssignmentDeductionsView(AssignmentDeductionsViewModel model)
{
    internal View Build() => VStack(Text("Deductions").FontSize(21),
        Text("Enter positive penalty points. Require instructor confirmation for policy findings or claims that need starter-file comparison."),
        Text("A single issue must not lose rubric points and incur an additional penalty twice. Unverified compilation or runtime behavior is marked unverified."),
        Button("Add deduction", () => model.Rows.Add(new(new AssignmentDeduction { Points = 10 }))),
        VStack(model.Rows.Select(row => VStack(FlexRow(TextField(row.Row.Name).AccessibilityLabel("Deduction rule").Flex(3),
            TextField(row.Row.Points).AccessibilityLabel("Penalty points").Width(90), Button("Remove", () => model.Rows.Remove(row))).Spacing(8),
            Toggle("Instructor confirmation required", row.Confirm)).Spacing(6).Id(row.Row.Id)).ToArray()).Spacing(14)).Spacing(12);
}
