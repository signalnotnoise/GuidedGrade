using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.ViewModels;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class FeedbackPanelView(FeedbackPanelViewModel model)
{ internal View Build() => Scroll(VStack(
            Text(model.StudentName).FontSize(18), Text(model.AssignmentTitle).FontSize(14),
            model.SavedReviews(),
            model.EarlierReviews(),
            Text("Feedback draft - export to keep a copy").FontSize(12),
            TextEditor(model.Draft).Height(model.Height.Value).UndoLimit(100).AccessibilityLabel("Editable feedback draft"),
            Picker(new[] { "TXT", "Markdown", "HTML" }, model.Format).AccessibilityLabel("Clipboard format"),
            HStack(Button(model.CopyLabel.Value, model.Copy).IsEnabled(!string.IsNullOrWhiteSpace(model.Draft.Value)), Button("Export feedback", model.Export)).Spacing(8),

            Toggle("Programming results and deductions", model.ShowResults),
            model.ShowResults.Value
                ? WpfUI.Native(() => model.Results).Height(350).Id("programming-results")
                : Text("").Id("programming-results-collapsed")
        ).Spacing(10).Padding(12));
}
