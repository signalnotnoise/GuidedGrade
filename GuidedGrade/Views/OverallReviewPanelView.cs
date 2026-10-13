using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class OverallReviewPanelView(OverallReviewPanelViewModel model)
{
    internal View Build() => VStack(model.Pending.Select(review => VStack(HStack(Text("Overall review · awaiting approval"),
        review.Issues.Count > 0 ? new ReviewWarningView(new ReviewWarningViewModel(review.Issues, () => System.Windows.MessageBox.Show(string.Join("\n\n", review.Issues), "Review validation details", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning))).Build() : Text("")),
        TextEditor(new Binding<string>(() => review.Explanation, value => review.Explanation = value)).Height(180).AccessibilityLabel("Editable overall review"), Button("Approve overall feedback", () => model.Approve(review)))).ToArray()).Spacing(8);
}
