using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class SavedAssignmentsView(SavedAssignmentsViewModel model, Action load, Action cancel)
{
    internal View Build() => VStack(Text("Load assignment").FontSize(22),
        Picker(model.Items.Select(a => a.Course + " / " + a.Title).ToArray(), model.Index).AccessibilityLabel("Saved assignments"),
        Text(model.Items.Count == 0 ? "No saved assignments yet." : "Loading replaces your current unsaved setup."),
        HStack(Button("Load selected assignment", load).IsEnabled(model.Selected != null), Button("Cancel", cancel)).Spacing(10)).Spacing(14).Padding(20);
}
