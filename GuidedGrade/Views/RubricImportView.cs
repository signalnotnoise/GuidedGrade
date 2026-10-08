using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class RubricImportView(RubricImportViewModel model, Action import, Action cancel)
{
    internal View Build() => Scroll(VStack(Text("Paste rubric").FontSize(22),
        Text("One criterion per line, points at the end. Example: Input validation 2.5"),
        TextEditor(model.Text).Height(300).AccessibilityLabel("Rubric text"), Text(model.Error.Value).Foreground("#FFABAB"),
        HStack(Button("Import", import), Button("Cancel", cancel)).Spacing(10)).Spacing(12).Padding(20));
}
