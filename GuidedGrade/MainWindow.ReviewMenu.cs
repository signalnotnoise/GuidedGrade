using System.Windows;
using GuidedGrade.Models;
using GuidedGrade.Services;
using GuidedGrade.Views;
using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade;
public partial class MainWindow
{
    private readonly State<bool> _reviewAssignmentSelected = new(false);
    private View BuildReviewMenu() => _reviewAssignmentSelected.Value
        ? new ReviewMenuView(new ReviewMenuViewModel(
            () => codeEditor.SelectionLength > 0 && _selectedTabButton != null,
            () => GetCheckedFiles(fileTreeView.Items).Any(f => !f.IsSolution),
            DesignBatchReview, () => GradeSection_Click(this, new()), () => AnalyzeWithLLM_Click(this, new()),
            () => ClearAssignmentReviews(false), () => ClearAssignmentReviews(true))).Build()
        : Text("");

    internal void ClearAssignmentReviews(bool all)
    {
        if (_currentAssignment == null) return;
        var assignment = ReviewContext.Snapshot(_currentAssignment)!;
        var paths = all ? null : GetCheckedFiles(fileTreeView.Items).Where(f => !f.IsSolution).Select(f => f.FullPath).ToArray();
        if (paths is { Length: 0 }) return;
        try
        {
            var removed = new Dictionary<string, List<SectionFeedback>>(StringComparer.OrdinalIgnoreCase);
            var candidates = paths ?? _commentPersistenceService.GetAllReviewedPaths().ToArray();
            foreach (var path in candidates)
                removed[path] = _commentPersistenceService.LoadComments(path).Where(c => ReviewContext.BelongsToAssignment(c.ReviewContext, assignment)).ToList();
            var affected = _commentPersistenceService.DeleteAssignmentReviews(assignment, paths);
            // Include checked/cache files even when their request has not persisted a result yet.
            foreach (var path in (paths ?? candidates.Concat(_fileComments.Keys).ToArray()).Distinct(StringComparer.OrdinalIgnoreCase))
                _reviewGeneration.Clear(path);
            foreach (var path in affected)
            {
                RefreshPersistedComments(path);
                foreach (var key in _reviewDrafts.Keys.Where(k => ReviewContext.BelongsToAssignment(k, assignment)).ToArray())
                    _reviewDrafts[key] = ReviewDraftCleanup.Remove(_reviewDrafts[key], removed[path]);
                _restoredFeedback.RemoveWhere(identity => identity.Contains("|" + path + "|", StringComparison.OrdinalIgnoreCase));
                RenderCommentsForFile(path);
            }
            if (_feedbackEditorKey != null && _feedbackEditor != null)
                _feedbackEditor.Value = _reviewDrafts.GetValueOrDefault(_feedbackEditorKey, "");
        }
        catch (Exception ex) { MessageBox.Show(this, "Could not clear reviews: " + ex.Message, "Clear reviews"); }
    }
}
