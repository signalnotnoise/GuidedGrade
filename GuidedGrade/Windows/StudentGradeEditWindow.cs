using System.Windows;
using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using GuidedGrade.Views;

namespace GuidedGrade.Windows;

internal sealed class StudentGradeEditWindow : ReviewWindow
{
    internal StudentGradeEditWindow(StudentGradeEditViewModel model)
    {
        Title = "Student grade";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowView(() => new StudentGradeEditView(model,
            clear => { if (model.Save(clear)) DialogResult = true; }, () => DialogResult = false).Build());
    }
}
