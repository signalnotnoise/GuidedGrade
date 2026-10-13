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
        private async Task<bool> GradeFileSectionsAsync(string filePath, string draftTarget,
            GradingAssignment assignment, string[] identifiers, string[] checkedPaths, string? searchRoot,
            LLMSettings settings, long reviewVersion)
        {
            if (assignment.Rubric.Count == 0)
                return false;

            var relatedFiles = RelatedFileResolver.FindRelatedFiles(filePath, checkedPaths, searchRoot);
            var fileText = await BoundedTextReader.ReadAsync(filePath);
            var sections = ExtractCodeSections(fileText, Path.GetFileNameWithoutExtension(filePath));

            if (sections.Count == 0)
                return false;

            var gradingService = new Services.SectionGradingService(assignment, settings);
            foreach (var section in sections)
            {
                var feedback = await gradingService.AnalyzeSectionAsync(
                    section.Name,
                    section.Code,
                    assignment.Rubric,
                    identifiers,
                    relatedFiles);

                if (!_reviewGeneration.IsCurrent(filePath, reviewVersion)) return false;
                feedback.StartLine = section.StartLine;
                feedback.EndLine = section.EndLine;
                TrackCommentForFile(filePath, feedback, draftTarget);
                if (draftTarget == CurrentFeedbackKey() && IsSelectedFile(filePath))
                    _commentLayer?.AddComment(feedback, section.StartLine, section.EndLine);
            }

            PersistCommentsForFile(filePath);

            if (sections.Count > 0 && draftTarget == CurrentFeedbackKey() && IsSelectedFile(filePath))
            {
                codeEditor.ScrollToLine(Math.Max(1, sections[0].StartLine));
            }
            return true;
        }

        private async Task GradeSelectedFilesWithSectionsAsync(List<Models.FileSystemItem> checkedFiles)
        {
            if (checkedFiles.Count == 0)
                return;

            var target = CurrentFeedbackKey(checkedFiles[0].FullPath);
            EnsureSingleReviewContext(checkedFiles.Select(file => file.FullPath), target);
            var assignment = ReviewContext.Snapshot(_currentAssignment);
            if (assignment == null) return;
            var settings = LLMSettings.Load();
            if (!ConfirmGrading(settings, $"Grade sections in {checkedFiles.Count} checked file(s)?")) return;
            var checkedPaths = checkedFiles.Select(file => file.FullPath).ToArray();
            var searchRoot = ReviewContext.SubmissionRoot(checkedPaths[0], (listBoxStudents.SelectedItem as Student)?.Folder);
            var inputs = checkedPaths.Select(path => (Path: path, Identifiers: GetStudentIdentifiers(path).ToArray(),
                Version: _reviewGeneration.Capture(path))).ToArray();
            _commentLayer?.ClearComments();

            var skipped = new List<string>();
            foreach (var file in inputs)
            {
                if (!await GradeFileSectionsAsync(file.Path, target, assignment, file.Identifiers, checkedPaths, searchRoot, settings, file.Version))
                    skipped.Add(Path.GetFileName(file.Path));
            }
            var message = $"Section comments completed for {checkedFiles.Count - skipped.Count} file(s).";
            if (skipped.Count > 0)
                message += "\n\nNo sections detected in: " + string.Join(", ", skipped) +
                    ".\nUse Overall feedback or select code and choose Grade Selected Section.";
            MessageBox.Show(message, "Section comments", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static List<CodeSection> ExtractCodeSections(string text, string name) => CodeSectionDetector.Extract(text, name);

        private async void AnalyzeSectionsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_currentAssignment == null || _currentAssignment.Rubric.Count == 0)
            {
                MessageBox.Show("Select an assignment with rubric items before requesting section comments.", "Section comments");
                return;
            }
            var files = GetCheckedFiles(fileTreeView.Items).Where(file => !file.IsSolution).ToList();
            if (files.Count == 0)
            {
                MessageBox.Show("Check the code files to review first.", "Section comments");
                return;
            }
            try { await GradeSelectedFilesWithSectionsAsync(files); }
            catch (Exception ex)
            {
                MessageBox.Show($"Section analysis failed: {ex.Message}", "Section comments", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AnalyzeWithLLM_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Collect all checked files from the tree
                var checkedFiles = GetCheckedFiles(fileTreeView.Items).Where(file => !file.IsSolution).ToList();

                if (checkedFiles.Count == 0)
                {
                    MessageBox.Show("Please select files to analyze by checking the boxes next to them.", 
                        "No Files Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var settings = Models.LLMSettings.Load();
                if (!ConfirmGrading(settings, $"Generate overall feedback for {checkedFiles.Count} checked file(s)?")) return;


                var feedbackTarget = CurrentFeedbackKey(checkedFiles[0].FullPath);
                EnsureSingleReviewContext(checkedFiles.Select(file => file.FullPath), feedbackTarget);
                var reviewFiles = CaptureOverallReviewFiles(checkedFiles.Select(file => file.FullPath));

                // Capture requirements and rubric before file reads or provider calls can yield.
                var identifiers = checkedFiles.SelectMany(file => GetStudentIdentifiers(file.FullPath)).Distinct().ToArray();
                var reviewAssignment = ReviewContext.Snapshot(_currentAssignment);
                var feedback = await ReviewOrchestrator.ReviewAsync(reviewAssignment, settings,
                    checkedFiles.Select(file => file.FullPath), identifiers);

                CompleteOverallFileReview(reviewFiles, feedbackTarget, feedback);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during LLM analysis: {ex.Message}");
                MessageBox.Show($"Error during analysis: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupAssignmentMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var setupWindow = new Windows.AssignmentSetupWindow(_assignmentPersistenceService, _currentAssignment)
            {
                Owner = this
            };

            if (setupWindow.ShowDialog() == true)
            {
                RefreshSavedAssignmentSelection(setupWindow.Assignment);
                ReloadSubmissionFolders();
                CloseSidePanel();
                MessageBox.Show($"Assignment '{setupWindow.Assignment.Title}' configured with {setupWindow.Assignment.Rubric.Count} rubric items.",
                    "Assignment Setup", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void GradeSection_Click(object sender, RoutedEventArgs e)
        {
            if (_currentAssignment == null)
            {
                var result = MessageBox.Show(
                    "No assignment has been configured.\n\nWould you like to set up an assignment now?",
                    "Setup Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SetupAssignmentMenuItem_Click(sender, e);
                }
                return;
            }

            // Get selected text and line numbers
            var selection = codeEditor.SelectedText;
            if (string.IsNullOrWhiteSpace(selection))
            {
                MessageBox.Show("Please select a code section (method) to grade.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var startLine = codeEditor.Document.GetLineByOffset(codeEditor.SelectionStart).LineNumber;
            var endLine = codeEditor.Document.GetLineByOffset(codeEditor.SelectionStart + codeEditor.SelectionLength).LineNumber;

            // Prompt for section name and rubric items
            var sectionDialog = new Windows.SectionGradingDialog(_currentAssignment.Rubric)
            {
                Owner = this
            };

            if (sectionDialog.ShowDialog() != true)
                return;

            var draftTarget = CurrentFeedbackKey();
            var sectionName = sectionDialog.SectionName;
            var relevantItems = sectionDialog.SelectedRubricItems;

            if (relevantItems.Count == 0)
            {
                MessageBox.Show("Please select at least one rubric item.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var settings = LLMSettings.Load();
            if (!ConfirmGrading(settings, $"Grade '{sectionName}'?")) return;

            try
            {
                var gradingService = new Services.SectionGradingService(_currentAssignment, settings);
                var currentPath = _selectedTabButton?.Tag as string;
                var reviewVersion = _reviewGeneration.Capture(currentPath);
                var feedback = await gradingService.AnalyzeSectionAsync(
                    sectionName,
                    selection,
                    relevantItems,
                    GetStudentIdentifiers(currentPath),
                    GetRelatedFilesForGrading(currentPath));

                if (!_reviewGeneration.IsCurrent(currentPath, reviewVersion)) return;
                feedback.StartLine = startLine;
                feedback.EndLine = endLine;
                if (!string.IsNullOrWhiteSpace(currentPath))
                {
                    TrackCommentForFile(currentPath, feedback, draftTarget);
                    PersistCommentsForFile(currentPath);
                }

                if (draftTarget == CurrentFeedbackKey() && currentPath != null && IsSelectedFile(currentPath))
                    _commentLayer?.AddComment(feedback, startLine, endLine);
                if (draftTarget == CurrentFeedbackKey() && currentPath != null && IsSelectedFile(currentPath))
                    codeEditor.ScrollToLine(Math.Max(1, startLine));
            }
            catch (Exception)
            {
                MessageBox.Show("Error during analysis. Please try again or review the LLM configuration.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task GradeFileWithRuntimeReportAsync(string filePath, string runtimeReport,
            GradingAssignment assignment, string[] identifiers, string[] checkedPaths, string? searchRoot, LLMSettings settings, string draftTarget, CancellationToken cancellationToken = default,
            bool? batchApprove = null, long? capturedReviewVersion = null)
        {
            if (assignment == null || assignment.Rubric.Count == 0)
                return;

            var reviewVersion = capturedReviewVersion ?? _reviewGeneration.Capture(filePath);
            if (!_reviewGeneration.IsCurrent(filePath, reviewVersion))
                throw new InvalidOperationException("Review was cleared after this job was queued.");
            var fileText = await BoundedTextReader.ReadAsync(filePath);
            var sections = ExtractCodeSections(fileText, Path.GetFileNameWithoutExtension(filePath));
            if (sections.Count == 0)
            {
                sections.Add(new CodeSection
                {
                    Name = Path.GetFileName(filePath),
                    Code = fileText,
                    StartLine = 1,
                    EndLine = fileText.Split('\n').Length
                });
            }

            var gradingService = new Services.SectionGradingService(assignment, settings);
            var relatedFiles = RelatedFileResolver.FindRelatedFiles(filePath, checkedPaths, searchRoot);
            foreach (var section in sections)
            {
                var feedback = await gradingService.AnalyzeSectionAsync(
                    section.Name,
                    section.Code,
                    assignment.Rubric,
                    identifiers,
                    relatedFiles,
                    runtimeReport, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                if (!_reviewGeneration.IsCurrent(filePath, reviewVersion)) return;
                feedback.StartLine = section.StartLine;
                feedback.EndLine = section.EndLine;
                feedback.ReviewContext = draftTarget;
                if (batchApprove == true) feedback.ReviewStatus = FeedbackReviewStatus.Approved;
                TrackCommentForFile(filePath, feedback, draftTarget, publishToDraft: batchApprove != false);
                PersistCommentsForFile(filePath);
                if (draftTarget == CurrentFeedbackKey() && IsSelectedFile(filePath))
                    RenderCommentsForFile(filePath);
            }

            PersistCommentsForFile(filePath);
        }

    }
}
