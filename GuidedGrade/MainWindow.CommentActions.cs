using GuidedGrade.ViewModels;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using GuidedGrade.Models;
using GuidedGrade.Services;
using GuidedGrade.Views;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;


namespace GuidedGrade
{
    public partial class MainWindow
    {
        private void CommentLayer_ApproveRequested(object? sender, Models.SectionFeedback feedback)
        {
            if (feedback.RubricReview is { LocationResolved: false }) return;
            feedback.ReviewStatus = Models.FeedbackReviewStatus.Approved;
            if (feedback.RubricReview != null)
            {
                feedback.RubricReview.EvidenceVerified = true;
                feedback.Issues.Remove("Model finding is unverified. Review the code, then approve, modify or reject.");
            }
            if (_selectedTabButton?.Tag is string filePath)
            {
                TrackCommentForFile(filePath, feedback, publishToDraft: false);
                PersistCommentsForFile(filePath);
                RestoreApprovedFeedback(filePath, _fileComments[filePath]);
                if (feedback.RubricReview != null) RefreshRubricGrade(feedback.ReviewContext);
                RenderCommentsForFile(filePath);
            }
        }

        private void CommentLayer_PlacementChanged(object? sender, Models.SectionFeedback feedback)
        {
            if (_selectedTabButton?.Tag is not string path || !MatchesCurrentReview(feedback)) return;
            try
            {
                PersistCommentsForFile(path);
                RenderCommentsForFile(path);
            }
            catch (Exception ex)
            {
                RefreshPersistedComments(path);
                RenderCommentsForFile(path);
                MessageBox.Show("Could not save the comment location: " + ex.Message, "Comment placement", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CommentLayer_RegenerateRequested(object? sender, Models.SectionFeedback feedback)
        {
            var draftTarget = CurrentFeedbackKey();
            if (_currentAssignment == null || _currentAssignment.Rubric.Count == 0)
            {
                MessageBox.Show("Load an assignment with a rubric before regenerating feedback.",
                    "Assignment Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var settings = LLMSettings.Load();
            if (!ConfirmGrading(settings, "Regenerate this section's feedback?")) return;
            try
            {
                var filePath = _selectedTabButton?.Tag as string;
                var sectionCode = GetSectionText(feedback);
                var gradingService = new Services.SectionGradingService(_currentAssignment, settings);
                var reviewVersion = _reviewGeneration.Capture(filePath);
                var regenerated = await gradingService.AnalyzeSectionAsync(
                    feedback.SectionName,
                    sectionCode,
                    _currentAssignment.Rubric,
                    GetStudentIdentifiers(filePath),
                    GetRelatedFilesForGrading(filePath));

                if (!_reviewGeneration.IsCurrent(filePath, reviewVersion)) return;
                regenerated.StartLine = feedback.StartLine;
                regenerated.EndLine = feedback.EndLine;
                regenerated.ReviewStatus = Models.FeedbackReviewStatus.Pending;

                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    TrackCommentForFile(filePath, regenerated, draftTarget);
                    PersistCommentsForFile(filePath);
                    RenderCommentsForFile(filePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error regenerating comment: {ex.Message}");
                MessageBox.Show("Unable to regenerate this comment. Check the LLM configuration and try again.",
                    "Regenerate Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CommentLayer_RejectRequested(object? sender, Models.SectionFeedback feedback)
        {
            if (_selectedTabButton?.Tag is not string filePath)
                return;

            feedback.ReviewStatus = Models.FeedbackReviewStatus.Rejected;
            if (_reviewDrafts.TryGetValue(feedback.ReviewContext, out var draft))
                _reviewDrafts[feedback.ReviewContext] = Services.ReviewDraftCleanup.Remove(draft, [feedback]);
            PersistCommentsForFile(filePath);
            if (feedback.RubricReview != null) RefreshRubricGrade(feedback.ReviewContext);
            RenderCommentsForFile(filePath);
        }

        private void TrackCommentForFile(string filePath, Models.SectionFeedback feedback, string? draftTarget = null, bool publishToDraft = true)
        {
            if (string.IsNullOrWhiteSpace(filePath) || feedback == null)
                return;

            if (publishToDraft) feedback.ReviewContext = draftTarget ?? CurrentFeedbackKey(filePath);
            _reviewWorkspace.Track(filePath, feedback);
            if (publishToDraft)
            {
                var text = Services.SavedFeedbackText.Format(feedback);
                MarkFeedbackImported(filePath, feedback, draftTarget);
                PresentGeneratedFeedback(draftTarget ?? CurrentFeedbackKey(), text);
            }
        }

        private void PersistCommentsForFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            if (_reviewWorkspace.SaveCached(filePath)) _savedReviewRevision.Value++;
        }

    }
}
