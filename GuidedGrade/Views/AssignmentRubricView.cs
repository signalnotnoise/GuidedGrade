using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class AssignmentRubricView(AssignmentRubricViewModel model, Action importRubric)
{
    internal View Build() => VStack(Text("Rubric").FontSize(21), Text(model.TotalLabel),
        HStack(Button("Add criterion", () => model.Rows.Add(new RubricEditorRow("", 10))), Button("Import rubric", importRubric)).Spacing(10),
        VStack(model.Rows.Select(row => FlexRow(TextField(row.Name).AccessibilityLabel("Criterion name").Flex(3),
            TextField(row.Points).AccessibilityLabel("Maximum points").Width(90), Button("Remove", () => model.Rows.Remove(row))).Spacing(8).Id(row.Id)).ToArray()).Spacing(10)).Spacing(12);
}
