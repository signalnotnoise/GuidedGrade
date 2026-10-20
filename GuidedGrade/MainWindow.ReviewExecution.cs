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
        private IReadOnlyList<string> GetStudentIdentifiers(string? filePath)
        {
            return Services.StudentDataSanitizer.GetIdentifiers(
                listBoxStudents.SelectedItem as Student,
                filePath,
                _openedDirectoryPath);
        }

        private IReadOnlyList<Services.RelatedSubmissionFile> GetRelatedFilesForGrading(string? filePath)
        {
            var extraPaths = new List<string>();

            try
            {
                extraPaths.AddRange(GetCheckedFiles(fileTreeView.Items).Select(item => item.FullPath));
            }
            catch
            {
            }

            extraPaths.AddRange(_fileTabs
                .Select(tab => tab.Tag as string)
                .Where(path => !string.IsNullOrWhiteSpace(path))!);

            return Services.RelatedFileResolver.FindRelatedFiles(
                filePath,
                extraPaths,
                ReviewContext.SubmissionRoot(filePath, (listBoxStudents.SelectedItem as Student)?.Folder));
        }

        private Services.SubmissionExecutionService CreateExecutionService()
            => new Services.SubmissionExecutionService(confirmLocal: warning =>
                MessageBox.Show(this, warning, "Run student code locally?",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);

        private bool ConfirmGrading(LLMSettings settings, string message) =>
            GradingConfirmation.IsAuthorized(settings, () => MessageBox.Show(this,
                message, "Start grading", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
        private async Task<string?> GetRuntimeExecutionReportAsync(
            string? filePath,
            bool forceExecution = false,
            IProgress<ConsoleProgress>? progress = null, SubmissionExecutionMode? executionMode = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            var settings = Models.LLMSettings.Load();
            if (executionMode.HasValue) settings.ExecutionMode = executionMode.Value;
            if (!forceExecution && !settings.ExecuteStudentSubmissions)
                return null;

            try
            {
                var service = CreateExecutionService();
                var requirements = _currentAssignment?.Requirements ?? settings.RequirementsTemplate;
                var report = await service.ExecuteAndFormatAsync(
                    filePath,
                    _gradingView.CurrentStudent?.Folder ?? _openedDirectoryPath,
                    requirements,
                    GetStudentIdentifiers(filePath),
                    GetRelatedFilesForGrading(filePath),
                    settings,
                    progress);
                return report == Services.SubmissionExecutionPolicy.LocalDeclined ? null : report;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Submission execution failed: {ex.Message}");
                return "# RUNTIME EXECUTION RESULTS\nExecution attempt failed: " + ex.Message;
            }
        }

        private string GetSectionText(Models.SectionFeedback feedback)
        {
            if (codeEditor.Document == null)
                return string.Empty;

            var lineCount = codeEditor.Document.LineCount;
            var startLine = Math.Clamp(Math.Max(feedback.StartLine, 1), 1, lineCount);
            var endLine = Math.Clamp(Math.Max(feedback.EndLine, startLine), 1, lineCount);
            var start = codeEditor.Document.GetLineByNumber(startLine);
            var end = codeEditor.Document.GetLineByNumber(endLine);
            return codeEditor.Document.GetText(start.Offset, end.EndOffset - start.Offset);
        }

    }
}
