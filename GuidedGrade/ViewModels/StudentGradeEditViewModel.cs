using GuidedGrade.Services;
using UI_Framework;

namespace GuidedGrade.ViewModels;

internal sealed class StudentGradeEditViewModel(string student, string assignment, StudentGrade? existing,
    double defaultMaximum, Action<StudentGrade?> save)
{
    internal string Student { get; } = student;
    internal string Assignment { get; } = assignment;
    internal bool CanClear => existing != null;
    internal State<string> Earned { get; } = new(existing?.Earned.ToString("0.##") ?? "");
    internal State<string> Possible { get; } = new((existing?.Possible ?? defaultMaximum).ToString("0.##"));
    internal State<string> Error { get; } = new("");

    internal bool Save(bool clear)
    {
        StudentGrade? grade = null;
        if (!clear)
        {
            if (!double.TryParse(Earned.Value, out var points) || !double.TryParse(Possible.Value, out var maximum) ||
                !double.IsFinite(points) || !double.IsFinite(maximum) || maximum <= 0 || points < 0 || points > maximum)
            { Error.Value = "Enter points from zero to a positive maximum."; return false; }
            grade = new(points, maximum);
        }
        try { save(grade); Error.Value = ""; return true; }
        catch (Exception ex) { Error.Value = "Could not save grade: " + ex.Message; return false; }
    }
}
