using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;

namespace GuidedGrade.Views;

internal sealed class StudentGradeEditView(StudentGradeEditViewModel model, Action<bool> save, Action cancel)
{
    internal View Build() => VStack(
        Text(model.Student).FontSize(21),
        Text(model.Assignment).Foreground("#AAB8C8"),
        Text("Final grade").FontSize(16),
        Text("Record this submission's grade. The course total updates automatically."),
        HStack(VStack(Text("Points earned"), TextField(model.Earned).AccessibilityLabel("Grade points earned")).Spacing(5),
            VStack(Text("Points possible"), TextField(model.Possible).AccessibilityLabel("Grade points possible")).Spacing(5)).Spacing(12),
        Text(model.Error.Value).Foreground("#FFABAB"),
        HStack(Button("Save grade", () => save(false)), Button("Clear grade", () => save(true)).IsEnabled(model.CanClear),
            Button("Cancel", cancel)).Spacing(8)
    ).Spacing(14).Padding(22);
}
