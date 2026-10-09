using GuidedGrade.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.Presentation;
using GuidedGrade.Views;
using GuidedGrade.ViewModels;
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
                (file.Context.Length == 0 || Services.ReviewContext.Matches(file.Context, Services.ReviewContext.Key(student.Folder, _currentAssignment, file.FilePath))))
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

    private View BuildEarlierReviews()
    {
        _ = _savedReviewRevision.Value;
        var comments = _selectedTabButton?.Tag is string path && _fileComments.TryGetValue(path, out var saved)
            ? saved.Where(c => c.ReviewContext.Length == 0 && c.ReviewStatus != Models.FeedbackReviewStatus.Rejected).ToArray() : [];
        var pending = _selectedTabButton?.Tag is string active && _fileComments.TryGetValue(active, out var records)
            ? records.Where(c => c.IsOverallReview && MatchesCurrentReview(c) && c.ReviewStatus == Models.FeedbackReviewStatus.Pending).ToArray() : [];
        return VStack(new OverallReviewPanelView(new OverallReviewPanelViewModel(pending, c => CommentLayer_ApproveRequested(this, c))).Build(),
            new EarlierReviewsView(new EarlierReviewsViewModel(comments)).Build()).Spacing(8);
    }

    private readonly ReviewDraftStore _reviewDrafts;
    private readonly HashSet<string> _restoredFeedback = new(StringComparer.Ordinal);
    private string FeedbackIdentity(string filePath, Models.SectionFeedback feedback, string? draftTarget = null) =>
        feedback.IsOverallReview
            ? (draftTarget ?? CurrentFeedbackKey()) + "|overall|" + feedback.SectionName + "|" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(feedback.Explanation)))
            : (draftTarget ?? CurrentFeedbackKey()) + "|" + filePath + "|" + feedback.StartLine + "|" + feedback.EndLine + "|" + feedback.SectionName;
    private void MarkFeedbackImported(string filePath, Models.SectionFeedback feedback, string? draftTarget = null)
    {
        if (_restoredFeedback.Count >= 4096) _restoredFeedback.Clear();
        _restoredFeedback.Add(FeedbackIdentity(filePath, feedback, draftTarget));
    }

    private void RestoreApprovedFeedback(string filePath, IEnumerable<Models.SectionFeedback> comments)
    {
        var key = CurrentFeedbackKey();
        var saved = comments.Where(c => MatchesCurrentReview(c) && c.ReviewStatus == Models.FeedbackReviewStatus.Approved).ToArray();
        if (saved.Length == 0 || listBoxStudents.SelectedItem is not Models.Student) return;
        foreach (var feedback in saved)
        {
            var identity = FeedbackIdentity(filePath, feedback);
            if (!_restoredFeedback.Add(identity)) continue;
            if (_restoredFeedback.Count > 4096) _restoredFeedback.Clear();
            var text = Services.SavedFeedbackText.Format(feedback);
            var previous = _reviewDrafts.GetValueOrDefault(key, "");
            if (previous.Contains(text, StringComparison.Ordinal)) continue;
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
        _reviewAssignmentSelected.Value = true;
        RefreshReviewSelection();
    }

    private void RefreshReviewSelection()
    {
        if (_logSelected.Value || _logModel.IsLoaded.Value) LoadAssignmentLog();
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
        var model = new ReviewRubricViewModel(assignment, () => WorkspaceAssignment_Click(this, new()));
        rubricDetailsTab.Content = _rubricHost = ReviewTheme.Host(new ReviewRubricView(model).Build);
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
            draft.Value = value;
            try { _reviewDrafts[draftKey] = value; _draftSaveError = null; }
            catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or InvalidOperationException) { _draftSaveError = ex.Message; copyLabel.Value = "Draft not saved â€” export feedback"; return; }
            copyLabel.Value = "Copy feedback";
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
        var model = new FeedbackPanelViewModel(student.FullName, assignmentTitle, () => SavedReviewLinks(student), draftBinding,
            _feedbackHeight, format, copyLabel, _showProgrammingResults, _gradingView, Copy, Export, BuildEarlierReviews);
        _feedbackHost = ReviewTheme.Host(new FeedbackPanelView(model).Build);
        commentsDetailsTab.Content = _feedbackHost;
        rightPanelTabs.SelectedItem = commentsDetailsTab;
        OpenSidePanel();
    }
}
