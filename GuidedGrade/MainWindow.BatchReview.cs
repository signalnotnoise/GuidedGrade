using System.IO;
using System.Windows;
using System.Windows.Media;
using GuidedGrade.Models;
using GuidedGrade.Presentation;
using GuidedGrade.Services;
using UI_Framework;
using static UI_Framework.UI;

namespace GuidedGrade;

public partial class MainWindow
{
    private readonly State<string> _batchProgress = new("");
    private string _batchFile = "", _batchEntry = "";
    private bool _batchApprove = true;
    private bool _batchBuildAndRun;

    private void DesignBatchReview()
    {
        if (_currentAssignment?.Rubric.Count is not > 0 || listBoxStudents.Items.Count == 0)
        {
            MessageBox.Show(this, "Choose an assignment with a rubric and open a folder of student submissions first.", "Batch review");
            return;
        }
        var assignment = ReviewContext.Snapshot(_currentAssignment)!;
        var students = listBoxStudents.Items.Cast<Student>().ToArray();
        var dialog = CreateBatchReviewDialog(assignment, students);
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    internal Window CreateBatchReviewDialog(GradingAssignment assignment, Student[] students)
    {
        var selected = listBoxStudents.SelectedItem as Student;
        var initialFile = _batchFile;
        if (initialFile.Length == 0 && selected?.Folder != null && _selectedTabButton?.Tag is string path && ReviewContext.Contains(selected.Folder, path))
            initialFile = Path.GetRelativePath(selected.Folder, path);
        var file = new State<string>(initialFile);
        var entry = new State<string>(_batchEntry);
        var approve = new State<bool>(_batchApprove);
        var buildAndRun = new State<bool>(_batchBuildAndRun);
        var preview = new State<string>("");
        var signature = new State<string>("");
        IReadOnlyList<BatchReviewItem>? plan = null;
        string Signature() => file.Value + "\n" + entry.Value + "\n" + approve.Value + "\n" + buildAndRun.Value;
        var dialog = new Window { Title = "Batch student review", Width = 650, Height = 720,
            WindowStartupLocation = WindowStartupLocation.CenterOwner };
        void Preview()
        {
            try
            {
                plan = BatchReviewPlan.Create(students, assignment, file.Value.Trim(), entry.Value.Trim(), buildAndRun.Value);
                preview.Value = string.Join("\n", plan.Select(p => p.Student.FullName + " — " + (p.SkipReason ?? $"Ready · {p.Files.Count} files\n  " +
                    string.Join("\n  ", p.Files.Select(path => Path.GetRelativePath(p.Student.Folder!, path) +
                        $" · modified {File.GetLastWriteTimeUtc(path):yyyy-MM-dd HH:mm:ss} UTC")))));
                signature.Value = Signature();
            }
            catch (Exception ex) { plan = null; signature.Value = ""; preview.Value = ex.Message; }
        }
        void Start()
        {
            if (plan == null || signature.Value != Signature()) return;
            var settings = LLMSettings.Load();
            if (settings.Provider == LLMProvider.OpenAI)
            { preview.Value = "Select a supported provider (Ollama or Azure OpenAI) in AI Provider settings."; return; }
            if (settings.Provider == LLMProvider.AzureOpenAI && (string.IsNullOrWhiteSpace(settings.AzureEndpoint) || string.IsNullOrWhiteSpace(settings.AzureApiKey) || string.IsNullOrWhiteSpace(settings.AzureDeployment)))
            { preview.Value = "Configure Azure OpenAI in AI Provider settings first."; return; }
            if (!ConfirmGrading(settings, $"Replace existing file reviews and {(buildAndRun.Value ? "build/run and " : "")}review {plan.Count(p => p.SkipReason == null)} students for {assignment.Title}?")) return;
            var captured = plan;
            var autoApprove = approve.Value;
            var execute = buildAndRun.Value;
            var versions = captured.SelectMany(p => p.Files).Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(path => path, path => _reviewGeneration.Capture(path), StringComparer.OrdinalIgnoreCase);
            if (!_aiTestQueue.TryEnqueue("Batch student review", token => RunBatchReviewAsync(captured, assignment, settings, autoApprove, execute, versions, token), out var completion))
            { preview.Value = "A batch is already queued/running, or the queue is full."; return; }
            _batchFile = file.Value; _batchEntry = entry.Value; _batchApprove = autoApprove;
            _batchBuildAndRun = execute;
            _ = ObserveBatchAsync(completion);
            dialog.Close();
            ShowQueue_Click(this, new());
        }
        var host = ReviewTheme.Host(() => Scroll(VStack(
            Text("Batch student review").FontSize(22),
            Text($"{assignment.Course} · {assignment.Title} · {students.Length} students"),
            Text("For each student: clear selected file reviews → optionally build/run → overall review each file → next student").FontSize(14),
            Text("Files to comment on, one per line, relative to each student's folder"),
            TextEditor(file).Height(75).AccessibilityLabel("Batch review files"),
            Text("Extra nested folders? Use **/Lab_2_Conversions/Lab 2/StudentWork.h\nFor multiple matches, the most recently modified file is used. Equal dates use alphabetical path order.").FontSize(12),
            Button("Use checked files from selected student", () =>
            {
                if (selected?.Folder == null) return;
                file.Value = string.Join("\n", GetCheckedFiles(fileTreeView.Items)
                    .Where(item => !item.IsSolution && ReviewContext.Contains(selected.Folder, item.FullPath))
                    .Select(item => Path.GetRelativePath(selected.Folder, item.FullPath)));
            }).IsEnabled(selected?.Folder != null),
            Text("Test entry point (optional relative .sln, project or runnable file)"),
            Toggle("Build and run before reviewing", buildAndRun).AccessibilityLabel("Batch build and run"),
            TextField(entry).AccessibilityLabel("Batch test entry point").IsEnabled(buildAndRun.Value),
            Text("Leave blank to detect the program from the review file. Uses the saved execution mode and build-confirmation setting.").FontSize(12),
            Toggle("Automatically approve comments and add them to feedback", approve),
            Text("Existing reviews on the selected files are cleared when each student starts. Feedback-only mode does not build or run code.").FontSize(12),
            Text("Otherwise, comments are saved inline for your approval. Missing files are skipped; errors are reported and the next student continues.").FontSize(12),
            Button("Preview students", Preview),
            TextEditor(new Binding<string>(() => preview.Value, _ => { })).IsReadOnly(true).UndoLimit(0).Height(170).AccessibilityLabel("Batch preview"),
            HStack(Button("Queue batch", Start).IsEnabled(signature.Value.Length > 0 && signature.Value == Signature() && plan?.Any(p => p.SkipReason == null) == true),
                Button("Cancel", () => dialog.Close())).Spacing(10)
        ).Spacing(10).Padding(20)));
        dialog.Content = host;
        dialog.Closed += (_, _) => host.Dispose();
        ReviewTheme.Apply(dialog);
        return dialog;
    }

    private async Task ObserveBatchAsync(Task completion)
    {
        try { await completion; }
        catch (OperationCanceledException) { _batchProgress.Value += "\nBatch cancelled. Completed comments are retained."; }
        catch (Exception ex) { _batchProgress.Value += "\nBatch stopped: " + ex.Message; }
    }

    private async Task RunBatchReviewAsync(IReadOnlyList<BatchReviewItem> plan, GradingAssignment assignment,
        LLMSettings settings, bool approve, bool buildAndRun, IReadOnlyDictionary<string, long> versions, CancellationToken token)
    {
        var results = new List<string>();
        var completed = 0;
        _batchProgress.Value = $"Starting batch for {plan.Count} students";
        await BatchReviewPlan.RunAsync(plan, async (item, cancellation) =>
        {
            var root = ReviewContext.SubmissionRoot(item.File, item.Student.Folder);
            var identifiers = item.Files.SelectMany(path => StudentDataSanitizer.GetIdentifiers(item.Student, path, null)).Distinct().ToArray();
            await BatchReviewPlan.ProcessStudentAsync(item, buildAndRun, path =>
            {
                if (!_reviewGeneration.IsCurrent(path, versions[path]))
                    throw new InvalidOperationException("Review was cleared after this batch was queued.");
                return ClearBatchFileReview(path, item.Context);
            }, async ct =>
            {
                using var progress = _runtimeTerminal.BeginSession();
                var report = await CreateExecutionService().ExecuteAndFormatAsync(item.EntryPoint!, root,
                    assignment.Requirements, identifiers, [], settings, progress, ct);
                ct.ThrowIfCancellationRequested();
                if (report == SubmissionExecutionPolicy.LocalDeclined) throw new InvalidOperationException("Local execution declined; no comments generated.");
                var reports = Path.Combine(AppDataPaths.LocalDirectory, "TestReports");
                Directory.CreateDirectory(reports);
                await File.WriteAllTextAsync(Path.Combine(reports, $"batch-{Guid.NewGuid():N}.txt"),
                    $"Submission: {item.EntryPoint}\n\n{report}", ct);
            }, async (path, version, ct) =>
            {
                var content = StudentDataSanitizer.Sanitize(await File.ReadAllTextAsync(path, ct), identifiers);
                var files = new List<OllamaService.CodeFile> { new() { Name = "file-1", Content = content } };
                var prompt = OverallFeedbackPrompt.WithFiles(
                    OverallFeedbackPrompt.BuildInstructions(assignment, settings.RequirementsTemplate, identifiers), files);
                var feedback = settings.Provider switch
                {
                    LLMProvider.AzureOpenAI => await new AzureOpenAIService(settings.AzureEndpoint, settings.AzureApiKey, settings.AzureDeployment)
                        .AnalyzeCodeAsync(prompt, [new CodeFile { FileName = "file-1", Content = content }], ct, wrapPrompt: false, jobTitle: "Batch overall file review"),
                    LLMProvider.Ollama => await new OllamaService(settings.OllamaBaseUrl, settings.SelectedModel)
                        .AnalyzeCodeAsync(files, prompt, wrapPrompt: false, cancellationToken: ct, jobTitle: "Batch overall file review"),
                    _ => throw new InvalidOperationException("Unsupported batch provider.")
                };
                ct.ThrowIfCancellationRequested();
                if (!_reviewGeneration.IsCurrent(path, version)) throw new InvalidOperationException("Review was cleared while this file was being reviewed.");
                if (string.IsNullOrWhiteSpace(feedback)) throw new InvalidOperationException("The model returned no feedback.");
                CompleteOverallFileReview([(path, version)], item.Context, feedback, approve, publishToDraft: approve);
            }, cancellation);
        }, (item, status) =>
        {
            if (status != "Running") { completed++; results.Add(item.Student.FullName + " — " + status); }
            _batchProgress.Value = $"{completed}/{plan.Count} processed · {item.Student.FullName}: {status}\n" + string.Join("\n", results);
        }, token);
        AppendToRuntimeTerminal("\nBatch review finished. See Job queue for student results.\n", Brushes.DeepSkyBlue);
        if (_currentAssignment?.Course == assignment.Course && _currentAssignment.Title == assignment.Title)
        {
            if (_selectedTabButton == null) OpenSavedReviewForSelectedStudent();
            else RefreshReviewSelection();
        }
    }

    internal long ClearBatchFileReview(string path, string context)
    {
        var comments = _fileComments.TryGetValue(path, out var cached) ? cached : _commentPersistenceService.LoadComments(path);
        _commentPersistenceService.DeleteComments(path);
        _reviewGeneration.Clear(path);
        _fileComments.Remove(path);
        foreach (var key in _reviewDrafts.Keys.ToArray())
        {
            var matching = comments.Where(c => ReviewContext.Matches(c.ReviewContext.Length == 0 ? context : c.ReviewContext, key));
            _reviewDrafts[key] = ReviewDraftCleanup.Remove(_reviewDrafts[key], matching);
        }
        _restoredFeedback.RemoveWhere(identity => identity.Contains("|" + path + "|", StringComparison.OrdinalIgnoreCase));
        if (_feedbackEditorKey != null && _feedbackEditor != null)
            _feedbackEditor.Value = _reviewDrafts.GetValueOrDefault(_feedbackEditorKey, "");
        if (IsSelectedFile(path)) _commentLayer?.ClearComments();
        return _reviewGeneration.Capture(path);
    }
}
