using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class AssignmentDetailsView(AssignmentDetailsViewModel model, Action<string> setCourse)
{
    internal View Build() => VStack(Text("Assignment instructions").FontSize(21),
        HStack(VStack(Text("Class"), TextField(new Binding<string>(() => model.Course.Value, setCourse)).AccessibilityLabel("Course name")),
            VStack(Text("Assignment title"), TextField(model.Title).AccessibilityLabel("Assignment title"))).Spacing(16),
        Text("Describe what the code must implement. Add points under Rubric and penalties under Deductions."),
        TextEditor(model.Requirements).Height(200).AccessibilityLabel("Assignment requirements"),
        Text("Review files — relative to each student's folder, one per line"),
        TextEditor(model.ReviewFiles).Height(90).AccessibilityLabel("Assignment review file paths"),
        Text("Use **/ for extra nesting, for example **/Week1/CaveMatchingGame/MatchingGame.cpp").FontSize(12),
        Text("Source history log — optional relative .fslog path"),
        TextField(model.LogPath).AccessibilityLabel("Assignment log path")).Spacing(12);
}
