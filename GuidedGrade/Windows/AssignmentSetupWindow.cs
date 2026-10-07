using System.Windows;
using GuidedGrade.Models;
using GuidedGrade.Services;
using GuidedGrade.Presentation;
using UI_Framework;
using static UI_Framework.UI;

namespace GuidedGrade.Windows;

public sealed class AssignmentSetupWindow : ReviewWindow
{
    private readonly AssignmentPersistenceService _persistence;
    private readonly State<string> _course = new("General");
    private readonly State<bool> _useFolderNames = new(false);
    private void SetCourse(string course)
    {
        _course.Value = course;
        _useFolderNames.Value = _persistence.UseFolderNames(string.IsNullOrWhiteSpace(course) ? "General" : course);
    }
    private readonly State<string> _title = new("");
    private readonly State<string> _requirements = new("");
    private readonly StateList<RubricEditorRow> _rows = new();
    private readonly StateList<string> _courses = new();
    private readonly StateList<GradingAssignment> _saved = new();
    private readonly State<int> _savedIndex = new(-1);
    public GradingAssignment Assignment { get; private set; } = new();

    public AssignmentSetupWindow() : this(new AssignmentPersistenceService()) { }

    internal AssignmentSetupWindow(AssignmentPersistenceService persistence, GradingAssignment? initial = null)
    {
        _persistence = persistence;
        SetCourse("General");
        Title = "Assignment setup"; Width = 800; Height = 820; MinWidth = 520; MinHeight = 420;
        foreach (var course in _persistence.GetCourseNames()) _courses.Add(course);
        if (initial != null) Load(initial);
        ShowView(BuildView);
    }

    public AssignmentSetupWindow(GradingAssignment? existingAssignment) : this(new AssignmentPersistenceService(), existingAssignment) { }

    private View BuildView() => Scroll(VStack(
        Text("Assignment and rubric").FontSize(22),
        Text("Course"), TextField(new Binding<string>(() => _course.Value, SetCourse)).AccessibilityLabel("Course name"),
        Picker(_courses.ToArray(), new Binding<int>(() => _courses.ToList().IndexOf(_course.Value), index =>
        { if (index >= 0 && index < _courses.Count) SetCourse(_courses[index]); })).AccessibilityLabel("Saved courses"),
        Toggle("Use folder names for this class", _useFolderNames).AccessibilityLabel("Use folder names for this class"),
        Text("On: list every immediate folder by its name, including repositories. Off: require Last_First-ID student folders. Applies to all assignments in this class."),
        Button("Find saved assignments", FindSaved),
        Picker(_saved.Select(item => item.Title).ToArray(), _savedIndex).AccessibilityLabel("Saved assignments"),
        Button("Load selected assignment", () => Load(_saved[_savedIndex.Value])).IsEnabled(_savedIndex.Value >= 0 && _savedIndex.Value < _saved.Count),
        Text("Assignment title"), TextField(_title).AccessibilityLabel("Assignment title"),
        Text("Requirements"), TextEditor(_requirements).Height(180).AccessibilityLabel("Assignment requirements"),
        Text("Rubric").FontSize(18),
        HStack(Button("Add criterion", () => _rows.Add(new RubricEditorRow("New criterion", 1))), Button("Import rubric", Import)).Spacing(8),
        VStack(_rows.Select(row => FlexRow(
            TextField(row.Name).AccessibilityLabel("Criterion name").Flex(3),
            TextField(row.Points).AccessibilityLabel("Maximum points").Width(90),
            Button("Remove", () => _rows.Remove(row)).Flex(0)
        ).Spacing(8).Id(row.Id)).ToArray()).Spacing(8),
        Text(TotalLabel()),
        HStack(Button("Save assignment", Save).ButtonStyle(ButtonStyleKind.Primary), Button("Cancel", () => DialogResult = false)).Spacing(8)
    ).Spacing(12).Padding(20));

    private string TotalLabel()
    {
        double total = 0;
        foreach (var row in _rows) { if (!row.TryPoints(out var points)) return "Enter positive points for each criterion."; total += points; }
        return $"Total: {total} points";
    }

    private void FindSaved()
    {
        _saved.Clear(); _savedIndex.Value = -1;
        foreach (var assignment in _persistence.GetAssignmentsByCourse(_course.Value.Trim())) _saved.Add(assignment);
        if (_saved.Count > 0) _savedIndex.Value = 0;
        else MessageBox.Show(this, "No saved assignments for this course.", "Assignment setup");
    }

    private void Load(GradingAssignment assignment)
    {
        SetCourse(assignment.Course); _title.Value = assignment.Title; _requirements.Value = assignment.Requirements;
        _rows.Clear();
        foreach (var item in assignment.Rubric) _rows.Add(new RubricEditorRow(item.Name, item.MaxPoints));
    }

    private void Import()
    {
        var dialog = new RubricImportWindow { Owner = this };
        if (dialog.ShowDialog() == true)
            foreach (var item in dialog.Items) _rows.Add(new RubricEditorRow(item.Name, item.MaxPoints));
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_title.Value) || _rows.Count == 0)
        { MessageBox.Show(this, "Enter an assignment title and at least one rubric criterion.", "Assignment setup"); return; }
        var items = new List<RubricItem>();
        foreach (var row in _rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name.Value) || !row.TryPoints(out var points))
            { MessageBox.Show(this, "Every criterion needs a name and finite positive points.", "Assignment setup"); return; }
            items.Add(new RubricItem(row.Name.Value.Trim(), points));
        }
        if (!double.IsFinite(items.Sum(item => item.MaxPoints)))
        { MessageBox.Show(this, "The total points are too large.", "Assignment setup"); return; }
        var assignment = new GradingAssignment
        {
            Course = string.IsNullOrWhiteSpace(_course.Value) ? "General" : _course.Value.Trim(),
            Title = _title.Value.Trim(), Requirements = _requirements.Value, Rubric = items
        };
        try { _persistence.SaveAssignment(assignment); _persistence.SaveFolderNames(assignment.Course, _useFolderNames.Value); Assignment = assignment; DialogResult = true; }
        catch (Exception ex) { MessageBox.Show(this, "Could not save the assignment: " + ex.Message, "Assignment setup"); }
    }
}
