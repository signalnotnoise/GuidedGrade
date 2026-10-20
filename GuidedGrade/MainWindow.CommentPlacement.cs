using System.IO;
using System.Text.Json;
using System.Windows;
using GuidedGrade.Models;
using GuidedGrade.Services;

namespace GuidedGrade;

public partial class MainWindow
{
    private void CommentLayer_ModifyRequested(object? sender, SectionFeedback feedback)
    {
        if (_selectedTabButton?.Tag is not string source || !MatchesCurrentReview(feedback)) return;
        var files = CommentDestinationFiles(source, feedback);
        // Editing a copy keeps Cancel and failed persistence from changing the visible record.
        var edited = JsonSerializer.Deserialize<SectionFeedback>(JsonSerializer.Serialize(feedback))!;
        var dialog = new Windows.InlineReviewEditWindow(edited, codeEditor.Document.LineCount,
            feedback.RubricReview?.MaximumPoints ?? (_currentAssignment?.TotalMaxPoints ?? 100), files,
            path => new ICSharpCode.AvalonEdit.Document.TextDocument(BoundedTextReader.Read(path)).LineCount) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var destination = dialog.DestinationFile ?? source;
        try
        {
            if (!CommentDestinationFiles(source, feedback).Contains(destination, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose a file in this student's current assignment.");
            _commentPersistenceService.MoveComment(source, destination, feedback, edited);
            if (_reviewDrafts.TryGetValue(feedback.ReviewContext, out var draft))
                _reviewDrafts[feedback.ReviewContext] = draft.Replace(SavedFeedbackText.Format(feedback), SavedFeedbackText.Format(edited), StringComparison.Ordinal);
            RefreshPersistedComments(source);
            if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) RefreshPersistedComments(destination);
            OpenFileInTab(destination);
            RenderCommentsForFile(destination);
            codeEditor.ScrollToLine(edited.StartLine);
            if (edited.ReviewStatus == FeedbackReviewStatus.Approved)
            {
                if (edited.RubricReview != null) RefreshRubricGrade(edited.ReviewContext);
                RestoreApprovedFeedback(destination, _fileComments[destination]);
            }
        }
        catch (Exception ex)
        {
            RefreshPersistedComments(source);
            RenderCommentsForFile(source);
            MessageBox.Show(this, "Could not save the comment: " + ex.Message, "Comment placement", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string[] CommentDestinationFiles(string source, SectionFeedback feedback)
    {
        if (feedback.ReviewContext.Length == 0 || feedback.IsPinned) return [source];
        IEnumerable<string> Walk(IEnumerable<FileSystemItem> items) => items.SelectMany(item => item.IsDirectory ? Walk(item.Children) : [item.FullPath]);
        return new[] { source }.Concat(Walk(fileTreeView.Items.OfType<FileSystemItem>())).Concat(_fileTabs.Select(tab => tab.Tag))
            .Distinct(StringComparer.OrdinalIgnoreCase).Where(path => File.Exists(path) &&
                ReviewContext.Matches(feedback.ReviewContext, CurrentFeedbackKey(path)))
            .OrderBy(path => string.Equals(path, source, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
