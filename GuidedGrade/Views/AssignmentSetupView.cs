using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class AssignmentSetupView(AssignmentSetupViewModel model, Action load, Action importPrompt, Action importRubric, Action save, Action cancel)
{
    internal View Build()
    {
        var body = model.Page.Value switch
        {
            1 => new AssignmentRubricView(model.Rubric, importRubric).Build(),
            2 => new AssignmentDeductionsView(model.Deductions).Build(),
            3 => new AssignmentFeedbackView(model.Feedback).Build(),
            4 => new ClassSettingsView(model.Class).Build(),
            _ => new AssignmentDetailsView(model.Details, model.SetCourse).Build()
        };
        return VStack(Text("Assignment setup").FontSize(24),
            HStack(Button("Load assignment", load), Button("Paste grading prompt", importPrompt)).Spacing(10),
            Picker(new[] { "Instructions", "Rubric", "Deductions", "Feedback", "Class settings" }, model.Page).AccessibilityLabel("Assignment setup section"),
            Scroll(body.Padding(4)).Height(470), Text(model.Error.Value).Foreground("#FFABAB"),
            HStack(Text(model.Rubric.TotalLabel), Button("Save assignment", save).ButtonStyle(ButtonStyleKind.Primary), Button("Cancel", cancel)).Spacing(12)).Spacing(14).Padding(20);
    }
}
