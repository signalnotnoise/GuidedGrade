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
        private async void BuildSolutionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var solutionPath = GetSelectedSolutionPath();
            if (solutionPath == null)
                return;

            try
            {
                var service = CreateExecutionService();
                var result = await service.BuildOnlyAsync(solutionPath);
                ShowBuildRunResult("Build", result);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Build failed: {ex.Message}", "Build", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowBuildRunResult(string operation, string result)
        {
            OpenRuntimeTerminalPanel();
            AppendToRuntimeTerminal($"\n{operation}\n{result}\n", Brushes.LightGray);
            // Keep diagnostics selectable and scrollable rather than growing a modal dialog.
            Title = $"GuidedGrade - {operation} finished; see Console for details";
        }

        private async void RunSolutionMenuItem_Click(object sender, RoutedEventArgs e)
            => await RunSelectedSolutionAsync(false);

        private async void BuildAndRunSolutionMenuItem_Click(object sender, RoutedEventArgs e)
            => await RunSelectedSolutionAsync(true);

        private async Task RunSelectedSolutionAsync(bool buildFirst)
        {
            var solutionPath = GetSelectedSolutionPath();
            if (solutionPath == null)
                return;

            try
            {
                var service = CreateExecutionService();
                var result = await service.LaunchAsync(solutionPath, buildFirst: buildFirst);
                ShowBuildRunResult(buildFirst ? "Build and Run" : "Run", result);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Run failed: {ex.Message}", "Run", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TestSolutionWithAiMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var solutionPath = GetSelectedSolutionPath();
            if (solutionPath == null) return;
            solutionPath = Path.GetFullPath(solutionPath);
            var settings = LLMSettings.Load();
            if ((sender as System.Windows.Controls.MenuItem)?.CommandParameter as string == "local")
            {
                settings.ExecutionMode = SubmissionExecutionMode.Local;
            }
            var searchRoot = ReviewContext.SubmissionRoot(solutionPath, (listBoxStudents.SelectedItem as Student)?.Folder);
            var identifiers = GetStudentIdentifiers(solutionPath).ToArray();
            var checkedPaths = GetCheckedFiles(fileTreeView.Items)
                .Where(file => !file.IsSolution && searchRoot != null && ReviewContext.Contains(searchRoot, file.FullPath)).Select(file => file.FullPath).ToArray();
            var assignment = ReviewContext.Snapshot(_currentAssignment);
            var draftTarget = CurrentFeedbackKey(solutionPath);
            if (assignment?.Rubric.Count > 0 && checkedPaths.Length > 0 &&
                !ConfirmGrading(settings, $"Test the solution and grade {checkedPaths.Length} checked file(s)?")) return;
            var requirements = assignment?.Requirements ?? settings.RequirementsTemplate;
            // Capture metadata only; load source and create the VM when the job starts.
            if (!_aiTestQueue.TryEnqueue(solutionPath, async cancellationToken =>
            {
                OpenRuntimeTerminalPanel();
                using var progress = _runtimeTerminal.BeginSession();
                AppendToRuntimeTerminal($"Testing: {solutionPath}\n", Brushes.DeepSkyBlue);
                var related = RelatedFileResolver.FindRelatedFiles(solutionPath, checkedPaths, searchRoot);
                var report = await CreateExecutionService().ExecuteAndFormatAsync(
                    solutionPath, searchRoot, requirements, identifiers, related, settings, progress, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (report == SubmissionExecutionPolicy.LocalDeclined) return;
                var reportDirectory = Path.Combine(AppDataPaths.LocalDirectory, "TestReports");
                Directory.CreateDirectory(reportDirectory);
                var reportPath = Path.Combine(reportDirectory, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt");
                await File.WriteAllTextAsync(reportPath, $"Submission: {solutionPath}\n\n{report}");
                if (assignment != null && assignment.Rubric.Count > 0)
                    foreach (var path in checkedPaths)
                        await GradeFileWithRuntimeReportAsync(path, report, assignment, identifiers, checkedPaths, searchRoot, settings, draftTarget, cancellationToken);
                AppendToRuntimeTerminal($"\nCompleted: {solutionPath}\nReport saved: {reportPath}\n", Brushes.DeepSkyBlue);
            }, out var completion))
            {
                MessageBox.Show("This solution is already running or queued, or the queue has reached its 50-test limit.",
                    "AI test queue", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Title = $"GuidedGrade - {_aiTestQueue.Count} AI test(s) running or queued";
            try { await completion; }
            catch (OperationCanceledException)
            {
                AppendToRuntimeTerminal($"Test cancelled ({solutionPath}).\n", Brushes.Goldenrod);
            }
            catch (Exception ex)
            {
                AppendToRuntimeTerminal($"Test failed ({solutionPath}): {ex.Message}\n", Brushes.Tomato);
                Debug.WriteLine($"AI test failed ({solutionPath}): {ex}");
            }
            finally { Title = $"GuidedGrade - {_aiTestQueue.Count} AI test(s) running or queued"; }
        }

        private string? GetSelectedSolutionPath()
        {
            return fileTreeView.SelectedItem is Models.FileSystemItem item && item.IsSolution
                ? item.FullPath
                : null;
        }

    }
}
