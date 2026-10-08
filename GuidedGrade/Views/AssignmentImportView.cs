using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class AssignmentImportView(AssignmentImportViewModel model, Action apply, Action cancel)
{
    internal View Build() => Scroll(VStack(Text("Import grading prompt").FontSize(22),
        Text("Paste your prompt. Preview the extracted criteria and penalties before applying."),
        TextEditor(model.Text).Height(220).AccessibilityLabel("Grading prompt"),
        Button("Preview import", model.Parse), Text(model.Error.Value).Foreground("#FFABAB"),
        TextEditor(new Binding<string>(() => model.Preview.Value, _ => { })).Height(260).IsReadOnly(true).AccessibilityLabel("Import preview"),
        HStack(Button("Apply import", apply).IsEnabled(model.CanApply), Button("Cancel", cancel)).Spacing(10)).Spacing(12).Padding(20));
}
