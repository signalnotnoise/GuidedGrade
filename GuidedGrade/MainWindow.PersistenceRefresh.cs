using GuidedGrade.Models;
namespace GuidedGrade;
public partial class MainWindow
{
    // Call only after the storage operation succeeds. Draft editors remain independent.
    private void RefreshPersistedComments(string path)
    {
        var persisted = _commentPersistenceService.LoadComments(path);
        _fileComments[path] = persisted;
        _savedReviewRevision.Value++;
    }

    internal void RefreshSavedAssignmentSelection(GradingAssignment saved)
    {
        var persisted = _assignmentPersistenceService.LoadAssignment(saved.Course, saved.Title)
            ?? throw new InvalidOperationException("The saved assignment could not be reloaded.");
        var courses = _assignmentPersistenceService.GetCourseNames();
        var assignments = _assignmentPersistenceService.GetAssignmentsByCourse(persisted.Course);
        _courses.ReplaceAll(courses);
        _assignments.ReplaceAll(assignments.Select(a => a.Title));
        _courseIndex.Value = courses.FindIndex(c => string.Equals(c, persisted.Course, StringComparison.OrdinalIgnoreCase));
        _assignmentIndex.Value = assignments.FindIndex(a => string.Equals(a.Title, persisted.Title, StringComparison.OrdinalIgnoreCase));
        SetReviewAssignment(persisted);
        if (rightPanelTabs.SelectedItem == rubricDetailsTab && sidePanelColumn.Width.Value > 0)
            WorkspaceRubric_Click(this, new());
    }
}
