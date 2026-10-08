using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class ClassSettingsView(ClassSettingsViewModel model)
{
    internal View Build() => VStack(Text("Class settings").FontSize(21),
        Toggle("Use folder names for this class", model.UseFolderNames).AccessibilityLabel("Use folder names for this class"),
        Text("On: list all immediate folders by name, including repositories. Off: require Last_First-ID student folders. Saved for all assignments in this class."),
        Text("Course review rules").FontSize(17),
        Text("Flag these rules with source evidence during grading. Configure numeric penalties separately under Deductions."),
        TextEditor(model.ReviewRules).Height(180).AccessibilityLabel("Course review rules"),
        Button("Use PG2 course rules", model.AddCppRules).IsEnabled(model.Course.Value.Equals("PG2", StringComparison.OrdinalIgnoreCase))).Spacing(14);
}
