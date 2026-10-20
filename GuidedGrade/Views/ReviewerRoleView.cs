using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class ReviewerRoleView(ReviewerRoleViewModel model)
{
    internal View Build() => VStack(Text("Reviewer role").FontSize(17),
        TextEditor(model.Text).Height(90).AccessibilityLabel("Reviewer role"),
        Text("Set the teaching perspective and tone for this assignment. Rubric scoring, evidence validation and privacy remain controlled by the app.").FontSize(12),
        new ToolbarActionView(new("Reset role", model.Reset, "Reset reviewer role", Hint: "Restore the default C++ instructor role")).Build()).Spacing(8);
}
