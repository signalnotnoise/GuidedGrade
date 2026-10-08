using GuidedGrade.Views;
using GuidedGrade.ViewModels;
using System.IO;
using System.Windows;
using GuidedGrade.Models;
using GuidedGrade.Presentation;
using GuidedGrade.Services;
using UI_Framework;
using static UI_Framework.UI;

namespace GuidedGrade;

public partial class MainWindow
{
    private readonly GradePersistenceService _gradePersistence;
    private readonly Dictionary<string, StudentGrade> _studentGrades;
    private readonly State<int> _gradeRevision = new(0);
    private string? _gradeLab;

    private bool CanEditGrade => listBoxStudents.SelectedItem is Student { Folder: not null } student &&
        _currentAssignment != null && _selectedTabButton?.Tag is string path && ReviewContext.Contains(student.Folder, path);

    private void RefreshGradeSelection()
    {
        if (CanEditGrade && listBoxStudents.SelectedItem is Student student)
            _gradeLab = Path.GetRelativePath(student.Folder!, ReviewContext.SubmissionRoot(_selectedTabButton!.Tag, student.Folder)!);
        _gradeRevision.Value++;
    }

    internal StudentGrade? GradeForStudent(Student student)
    {
        if (_gradeLab == null || student.Folder == null || _currentAssignment == null) return null;
        // Use a synthetic child file so a missing lab directory still produces its own scope.
        var path = Path.Combine(student.Folder, _gradeLab, "__grade_scope__");
        return _studentGrades.GetValueOrDefault(ReviewContext.Key(student.Folder, _currentAssignment, path));
    }

    internal StudentGrade? TotalForStudent(Student student) => GradeTotals.ForCourse(_studentGrades, student.Folder, _currentAssignment?.Course);

    internal void SaveStudentGrade(string context, StudentGrade? grade)
    {
        _gradePersistence.Save(context, grade);
        // Publish a complete persisted snapshot so course totals include other saved grades.
        var persisted = _gradePersistence.LoadAll();
        _studentGrades.Clear();
        foreach (var entry in persisted) _studentGrades.Add(entry.Key, entry.Value);
        _gradeRevision.Value++;
    }

    private static string GradeColor(StudentGrade? grade) => new GradeBadgeViewModel(grade, "").Color;
    private View BuildStudentGradeRow(Student student)
    {
        _ = _gradeRevision.Value;
        return new StudentGradeRowView(new StudentGradeRowViewModel(student.FullName, GradeForStudent(student), TotalForStudent(student))).Build();
    }

    private View BuildGradeScope()
    {
        _ = _gradeRevision.Value;
        return VStack(Text("SUBMISSIONS").FontSize(12),
            Text(_gradeLab == null ? "Open a file to view assignment grades" : (_currentAssignment?.Title ?? "Choose assignment") + " · " + (_gradeLab == "." ? "Submission" : _gradeLab))
                .FontSize(11).Foreground("#AAB8C8")).Spacing(4).Padding(10);
    }

    private View BuildGradeStatus()
    {
        _ = _gradeRevision.Value;
        var grade = CanEditGrade ? _studentGrades.GetValueOrDefault(CurrentFeedbackKey()) : null;
        var total = listBoxStudents.SelectedItem is Student student ? TotalForStudent(student) : null;
        return HStack(new Views.ToolbarActionView(new ViewModels.ToolbarActionViewModel(
            CanEditGrade ? "Assignment: " + (grade?.Summary ?? "Not graded") : "Grade: select an assignment and file",
            EditStudentGrade, "Edit student grade", CanEditGrade, GradeColor(grade))).Build(),
            new Views.ToolbarLabelView(new ViewModels.ToolbarLabelViewModel("Course total: " + (total?.Summary ?? "Not graded"))).Build()
                .Foreground(GradeColor(total)).AccessibilityLabel("Student course total")).Spacing(8);
    }

    private void EditStudentGrade()
    {
        if (!CanEditGrade || listBoxStudents.SelectedItem is not Student student) return;
        // Capture identity before opening the editor; saving never follows a later selection.
        var key = CurrentFeedbackKey();
        var existing = _studentGrades.GetValueOrDefault(key);
        var earned = new State<string>(existing?.Earned.ToString("0.##") ?? "");
        var possible = new State<string>((existing?.Possible ?? (_currentAssignment!.TotalMaxPoints > 0 ? _currentAssignment.TotalMaxPoints : 100)).ToString("0.##"));
        var error = new State<string>("");
        var dialog = new Window { Owner = this, Title = "Student grade", Width = 440, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        void Save(bool clear)
        {
            StudentGrade? grade = null;
            if (!clear)
            {
                if (!double.TryParse(earned.Value, out var points) || !double.TryParse(possible.Value, out var maximum) ||
                    !double.IsFinite(points) || !double.IsFinite(maximum) || maximum <= 0 || points < 0 || points > maximum)
                { error.Value = "Enter points from zero to a positive maximum."; return; }
                grade = new(points, maximum);
            }
            try { SaveStudentGrade(key, grade); dialog.Close(); }
            catch (Exception ex) { error.Value = "Could not save grade: " + ex.Message; }
        }
        using var host = ReviewTheme.Host(() => VStack(
            Text(student.FullName).FontSize(21),
            Text(_currentAssignment!.Title + " · " + (_gradeLab == "." ? "Submission" : _gradeLab)).Foreground("#AAB8C8"),
            Text("Final grade").FontSize(16),
            Text("Record this submission's grade. The course total updates automatically."),
            HStack(VStack(Text("Points earned"), TextField(earned).AccessibilityLabel("Grade points earned")).Spacing(5),
                VStack(Text("Points possible"), TextField(possible).AccessibilityLabel("Grade points possible")).Spacing(5)).Spacing(12),
            Text(error.Value).Foreground("#FFABAB"),
            HStack(Button("Save grade", () => Save(false)), Button("Clear grade", () => Save(true)).IsEnabled(existing != null),
                Button("Cancel", () => dialog.Close())).Spacing(8)
        ).Spacing(14).Padding(22));
        dialog.Content = host;
        ReviewTheme.Apply(dialog);
        dialog.ShowDialog();
    }
}
