using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.Presentation;
using static UI_Framework.UI;

namespace GuidedGrade;

public partial class MainWindow
{
    private readonly State<int> _savedReviewRevision = new(0);
    private View SavedReviewLinks(Models.Student student)
    {
        _ = _savedReviewRevision.Value;
        return VStack(SavedReviewFilesForSelectedStudent().Select(path => Button("Open review: " + System.IO.Path.GetRelativePath(student.Folder!, path),
            () => { OpenFileInTab(path); RefreshReviewSelection(); codeEditor.ScrollToHome(); }).Id("saved-" + path)).ToArray()).Spacing(4);
    }
    private string[] SavedReviewFilesForSelectedStudent()
    {
        if (listBoxStudents.SelectedItem is not Models.Student { Folder: not null } student || _currentAssignment == null) return [];
        return _commentPersistenceService.GetReviewedFiles()
            .Where(file => Services.ReviewContext.Contains(student.Folder, file.FilePath) &&
                Services.ReviewContext.Matches(file.Context, Services.ReviewContext.Key(student.Folder, _currentAssignment, file.FilePath)))
            .Select(file => file.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Where(System.IO.File.Exists).ToArray();
    }

    internal bool OpenSavedReviewForSelectedStudent()
    {
        var file = SavedReviewFilesForSelectedStudent().FirstOrDefault();
        if (file == null) return false;
        OpenFileInTab(file);
        // Re-selecting an already-open file still refreshes persisted/cache comments.
        RefreshReviewSelection();
        codeEditor.ScrollToHome();
        return true;
    }

    private readonly Dictionary<string, string> _reviewDrafts = new();
    private readonly HashSet<string> _restoredFeedback = new(StringComparer.Ordinal);
    private string FeedbackIdentity(string filePath, Models.SectionFeedback feedback, string? draftTarget = null) =>
        (draftTarget ?? CurrentFeedbackKey()) + "|" + filePath + "|" + feedback.StartLine + "|" + feedback.EndLine + "|" + feedback.SectionName;
    private void MarkFeedbackImported(string filePath, Models.SectionFeedback feedback, string? draftTarget = null)
        => _restoredFeedback.Add(FeedbackIdentity(filePath, feedback, draftTarget));

    private void RestoreApprovedFeedback(string filePath, IEnumerable<Models.SectionFeedback> comments)
    {
        var key = CurrentFeedbackKey();
        var saved = comments.Where(c => MatchesCurrentReview(c) && c.ReviewStatus == Models.FeedbackReviewStatus.Approved).ToArray();
        if (saved.Length == 0 || listBoxStudents.SelectedItem is not Models.Student) return;
        foreach (var feedback in saved)
        {
            var identity = FeedbackIdentity(filePath, feedback);
            if (!_restoredFeedback.Add(identity)) continue;
            var text = Services.SavedFeedbackText.Format(feedback);
            var previous = _reviewDrafts.GetValueOrDefault(key, "");
            _reviewDrafts[key] = string.IsNullOrWhiteSpace(previous) ? text : previous + "\n\n---\n\n" + text;
        }
        // A second file in the same lab shares the existing editor. Refresh its
        // binding as well as the backing draft, preserving instructor edits.
        if (_feedbackEditorKey == key && _feedbackEditor != null)
            _feedbackEditor.Value = _reviewDrafts.GetValueOrDefault(key, "");
        WorkspaceFeedback_Click(this, new RoutedEventArgs());
    }
    private State<string>? _feedbackEditor;
    private ViewHost? _feedbackHost;
    private ViewHost? _rubricHost;
    private readonly State<double> _feedbackHeight = new(240);
    private readonly State<bool> _showProgrammingResults = new(false);
    private string? _feedbackEditorKey;
    internal string CurrentFeedbackKey(string? path = null) => Services.ReviewContext.Key(
        (listBoxStudents.SelectedItem as Models.Student)?.Folder, _currentAssignment,
        path ?? _selectedTabButton?.Tag as string);

    private bool MatchesCurrentReview(Models.SectionFeedback feedback) =>
        Services.ReviewContext.Matches(feedback.ReviewContext, CurrentFeedbackKey()) ||
        (feedback.ReviewContext.Length == 0 && _currentAssignment == null);

    internal void SetReviewAssignment(Models.GradingAssignment assignment)
    {
        _currentAssignment = assignment;
        RefreshReviewSelection();
    }

    private void RefreshReviewSelection()
    {
        RefreshGradeSelection();
        if (_selectedTabButton?.Tag is string path) LoadCommentsForFile(path);
        if (_feedbackEditorKey != null && _feedbackEditorKey != CurrentFeedbackKey())
            WorkspaceFeedback_Click(this, new RoutedEventArgs());
    }

    private void PresentGeneratedFeedback(string key, string text)
    {
        var previous = _reviewDrafts.GetValueOrDefault(key, "");
        _reviewDrafts[key] = string.IsNullOrWhiteSpace(previous) ? text : previous + "\n\n---\n\n" + text;
        if (key != CurrentFeedbackKey()) return;
        if (_feedbackEditor != null && _feedbackEditorKey == key)
        {
            _feedbackEditor.Value = _reviewDrafts[key];
            if (_panelPreferences.ShowComments) { rightPanelTabs.SelectedItem = commentsDetailsTab; OpenSidePanel(); }
        }
        else WorkspaceFeedback_Click(this, new RoutedEventArgs());
    }

    private void WorkspaceAssignment_Click(object sender, RoutedEventArgs e)
    {
        SetupAssignmentMenuItem_Click(sender, e);
        WorkspaceRubric_Click(sender, e);
    }

    private void WorkspaceRubric_Click(object sender, RoutedEventArgs e)
    {
        var assignment = _currentAssignment;
        _rubricHost?.Dispose();
        rubricDetailsTab.Content = _rubricHost = ReviewTheme.Host(() => Scroll(VStack(
            Text(assignment?.Title ?? "Assignment and rubric").FontSize(22),
            Text(assignment?.Requirements ?? "Choose a saved assignment or set one up to define expectations."),
            Text("Rubric").FontSize(18),
            VStack(assignment?.Rubric.Select((item, index) => Text($"{item.Name}: {item.MaxPoints} points").Id(index.ToString())).ToArray() ?? [] ).Spacing(10),
            HStack(Button("Set up assignment", () => WorkspaceAssignment_Click(this, new())),
                Button("Close panel", CloseSidePanel)).Spacing(8)
        ).Spacing(14).Padding(18)));
        rightPanelTabs.SelectedItem = rubricDetailsTab;
        OpenSidePanel();
    }

    private void WorkspaceFeedback_Click(object sender, RoutedEventArgs e)
    {
        if (!_panelPreferences.ShowComments) return;
        if (_selectedTabButton == null && OpenSavedReviewForSelectedStudent()) return;
        if (listBoxStudents.SelectedItem is not Models.Student student)
        {
            _feedbackHost?.Dispose(); _feedbackHost = null; _feedbackEditor = null; _feedbackEditorKey = null;
            commentsDetailsTab.Content = _feedbackHost = ReviewTheme.Host(() => Text("Select a student to prepare comments and feedback.").Padding(20));
            rightPanelTabs.SelectedItem = commentsDetailsTab;
            OpenSidePanel();
            return;
        }
        var draftKey = CurrentFeedbackKey();
        if (_feedbackEditorKey == draftKey && _feedbackHost != null)
        {
            rightPanelTabs.SelectedItem = commentsDetailsTab;
            OpenSidePanel();
            return;
        }
        _feedbackHost?.Dispose();
        var assignmentTitle = _currentAssignment?.Title ?? "Submission feedback";
        var draft = new State<string>(_reviewDrafts.GetValueOrDefault(draftKey, ""));
        var format = new State<int>(0);
        var copyLabel = new State<string>("Copy feedback");
        _feedbackEditor = draft; _feedbackEditorKey = draftKey;
        var draftBinding = new Binding<string>(() => draft.Value, value =>
        {
            draft.Value = value; _reviewDrafts[draftKey] = value; copyLabel.Value = "Copy feedback";
        });
        void Copy()
        {
            try
            {
                Clipboard.SetText(Services.FeedbackCopyFormatter.Format(draft.Value, new[] { "TXT", "Markdown", "HTML" }[format.Value]));
                copyLabel.Value = "Copied!";
            }
            catch (System.Runtime.InteropServices.ExternalException)
            { MessageBox.Show(this, "The clipboard is busy. Please try copying again.", "Copy feedback"); }
        }
        void Export()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Text document|*.txt", FileName = "feedback.txt" };
            if (dialog.ShowDialog(this) != true) return;
            try { System.IO.File.WriteAllText(dialog.FileName, student.FullName + "\n" + assignmentTitle + "\n\n" + draft.Value); }
            catch (Exception ex) { MessageBox.Show(this, "Could not export feedback: " + ex.Message, "Export feedback"); }
        }
        _feedbackHost = ReviewTheme.Host(() => Scroll(VStack(
            Text(student.FullName).FontSize(18), Text(assignmentTitle).FontSize(14),
            SavedReviewLinks(student),
            Text("Feedback draft - export to keep a copy").FontSize(12),
            TextEditor(draftBinding).Height(_feedbackHeight.Value).UndoLimit(100).AccessibilityLabel("Editable feedback draft"),
            Picker(new[] { "TXT", "Markdown", "HTML" }, format).AccessibilityLabel("Clipboard format"),
            HStack(Button(copyLabel.Value, Copy).IsEnabled(!string.IsNullOrWhiteSpace(draft.Value)), Button("Export feedback", Export)).Spacing(8),
            Button("Close feedback", CloseSidePanel),
            Toggle("Programming results and deductions", _showProgrammingResults),
            _showProgrammingResults.Value
                ? WpfUI.Native(() => _gradingView).Height(350).Id("programming-results")
                : Text("").Id("programming-results-collapsed")
        ).Spacing(10).Padding(12)));
        commentsDetailsTab.Content = _feedbackHost;
        rightPanelTabs.SelectedItem = commentsDetailsTab;
        OpenSidePanel();
    }
}
