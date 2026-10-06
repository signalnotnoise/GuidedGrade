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
    public partial class MainWindow : Window
    {
        private readonly GradingView _gradingView = new();
        private readonly AiTestQueue _aiTestQueue = new();
        private readonly RuntimeTerminalPresenter _runtimeTerminal;
        private readonly Services.AssignmentPersistenceService _assignmentPersistenceService;

        private FileTab? _selectedTabButton;
        private ViolationHighlighter _violationHighlighter = null!;
        private double _sidePanelWidth = 500;
        private string? _openedDirectoryPath;

        // Section grading
        private Models.GradingAssignment? _currentAssignment;
        private Controls.InlineCommentLayer? _commentLayer;
        private readonly Services.CommentPersistenceService _commentPersistenceService;
        private readonly Dictionary<string, List<Models.SectionFeedback>> _fileComments = new(StringComparer.OrdinalIgnoreCase);


        public MainWindow() : this(new AssignmentPersistenceService(), new CommentPersistenceService()) { }

        internal MainWindow(AssignmentPersistenceService assignments, CommentPersistenceService comments)
        {
            _assignmentPersistenceService = assignments;
            _commentPersistenceService = comments;
            _gradePersistence = new GradePersistenceService(assignments.DatabasePath);
            _studentGrades = _gradePersistence.LoadAll();
            InitializeFrameworkShell();
            InitializeJobQueuePanel();
            ApplyPanelPreferences();
            _runtimeTerminal = new RuntimeTerminalPresenter(runtimeTerminalRichTextBox);
            Closed += (_, _) => _runtimeTerminal.Dispose();
            LoadMonokaiTheme();
            SetupInlineComments();
            LoadSavedCourseOptions();
        }

        private void LoadSavedCourseOptions()
        {
            var courses = _assignmentPersistenceService.GetCourseNames();
            _courses.ReplaceAll(courses);

            if (courses.Count > 0)
            {
                SelectCourse(0);
            }
        }

        private void LoadSavedAssignmentsForCourse(string course)
        {
            _assignments.Clear();
            _assignmentIndex.Value = -1;
            if (string.IsNullOrWhiteSpace(course))
                return;

            var assignments = _assignmentPersistenceService.GetAssignmentsByCourse(course);
            foreach (var assignment in assignments)
            {
                _assignments.Add(assignment.Title);
            }

            if (_assignments.Count > 0)
            {
                SelectAssignment(0);
            }
        }

        private void SelectCourse(int index)
        {
            _courseIndex.Value = index;
            if (index < 0 || index >= _courses.Count) return;
            LoadSavedAssignmentsForCourse(_courses[index]);
        }

        private void SelectAssignment(int index)
        {
            _assignmentIndex.Value = index;
            if (index < 0 || index >= _assignments.Count || _courseIndex.Value < 0 || _courseIndex.Value >= _courses.Count) return;
            var selectedTitle = _assignments[index];
            var course = _courses[_courseIndex.Value];

            var assignment = _assignmentPersistenceService.LoadAssignment(course, selectedTitle);
            if (assignment == null)
                return;

            SetReviewAssignment(assignment);
            CloseSidePanel();
            MessageBox.Show($"Loaded assignment '{assignment.Title}' for course '{assignment.Course}'.",
                "Saved Assignment Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // --- Theme ------------------------------------------------------------

        private void LoadMonokaiTheme()
        {
            _violationHighlighter = new ViolationHighlighter(codeEditor);

            var uri = new Uri("pack://application:,,,/GuidedGrade;component/Resources/Monokai.xshd");
            using var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream == null) return;

            using var reader = new System.Xml.XmlTextReader(stream);
            codeEditor.SyntaxHighlighting = HighlightingLoader
                .Load(reader, HighlightingManager.Instance);

            codeEditor
                .TextArea
                .TextView
                .BackgroundRenderers
                .Add(_violationHighlighter);
        }

        private void SetupInlineComments()
        {
            _commentLayer = new Controls.InlineCommentLayer(codeEditor);
            _commentLayer.ApproveRequested += CommentLayer_ApproveRequested;
            _commentLayer.RegenerateRequested += CommentLayer_RegenerateRequested;
            _commentLayer.RejectRequested += CommentLayer_RejectRequested;

            if (commentOverlay != null)
            {
                commentOverlay.Children.Add(_commentLayer);
                Panel.SetZIndex(_commentLayer, 10);
                commentOverlay.Width = codeEditor.ActualWidth;
                commentOverlay.Height = codeEditor.ActualHeight;
            }

            codeEditor.SizeChanged += (_, _) =>
            {
                if (commentOverlay != null)
                {
                    commentOverlay.Width = codeEditor.ActualWidth;
                    commentOverlay.Height = codeEditor.ActualHeight;
                }

                _commentLayer?.UpdateCommentPositions();
            };
        }

        private async Task<bool> GradeFileSectionsAsync(string filePath, string draftTarget,
            GradingAssignment assignment, string[] identifiers, string[] checkedPaths, string? searchRoot,
            LLMSettings settings, long reviewVersion)
        {
            if (assignment.Rubric.Count == 0)
                return false;

            var relatedFiles = RelatedFileResolver.FindRelatedFiles(filePath, checkedPaths, searchRoot);
            var fileText = await File.ReadAllTextAsync(filePath);
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

        private List<CodeSection> ExtractCodeSections(string fileText, string defaultName)
        {
            var sections = new List<CodeSection>();
            var controlStatementNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "if", "else", "for", "foreach", "while", "do", "switch", "case", "catch", "using", "lock"
            };
            var text = fileText.Replace("\r\n", "\n");
            var lines = text.Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var match = System.Text.RegularExpressions.Regex.Match(
                    line,
                    @"(?i)(?:^|\s)(?:[A-Za-z_:][\w:<>\s*&]*\s+)?[A-Za-z_][A-Za-z0-9_]*\s*\([^;{}]*\)\s*(?:const)?\s*\{");

                if (!match.Success)
                    continue;

                var functionName = line.Trim();
                var startLine = i + 1;
                var braceIndex = line.IndexOf('{');
                if (braceIndex < 0)
                    continue;

                var bodyStart = text.IndexOf('{', i == 0 ? 0 : text.IndexOf(lines[i], StringComparison.Ordinal));
                if (bodyStart < 0)
                    continue;

                var openBraces = 0;
                var closeBraces = 0;
                var startOffset = bodyStart;
                var foundStart = false;
                var endOffset = -1;

                for (var offset = bodyStart; offset < text.Length; offset++)
                {
                    if (text[offset] == '{')
                    {
                        openBraces++;
                        foundStart = true;
                    }
                    else if (text[offset] == '}')
                    {
                        closeBraces++;
                        if (foundStart && openBraces == closeBraces)
                        {
                            endOffset = offset;
                            break;
                        }
                    }
                }

                if (endOffset < 0)
                    continue;

                var sectionText = text.Substring(startOffset + 1, endOffset - startOffset - 1);
                var sectionEndLine = CountLines(text.Substring(0, endOffset + 1));

                var sectionName = ExtractFunctionName(functionName);
                // Control statements are not independent grading sections.
                if (controlStatementNames.Contains(sectionName))
                    continue;

                if (string.IsNullOrWhiteSpace(sectionName))
                    sectionName = $"{defaultName} - section {sections.Count + 1}";

                sections.Add(new CodeSection
                {
                    Name = sectionName,
                    Code = sectionText,
                    StartLine = startLine,
                    EndLine = sectionEndLine
                });
            }

            return sections;
        }

        private static string ExtractFunctionName(string signature)
        {
            var match = System.Text.RegularExpressions.Regex.Match(signature, @"([A-Za-z_][A-Za-z0-9_]*)\s*\(");
            return match.Success ? match.Groups[1].Value : signature.Trim();
        }

        private static int CountLines(string value)
        {
            return string.IsNullOrEmpty(value) ? 0 : value.Split('\n').Length;
        }

        private class CodeSection
        {
            public string Name { get; set; } = "";
            public string Code { get; set; } = "";
            public int StartLine { get; set; }
            public int EndLine { get; set; }
        }

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
                var instructions = OverallFeedbackPrompt.BuildInstructions(_currentAssignment, settings.RequirementsTemplate, identifiers);

                var codeFiles = new List<Services.OllamaService.CodeFile>();
                foreach (var file in checkedFiles)
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(file.FullPath);
                        codeFiles.Add(new Services.OllamaService.CodeFile
                        {
                            Name = Services.StudentDataSanitizer.AnonymousFileName(codeFiles.Count + 1, Path.GetExtension(file.Name)),
                            Content = Services.StudentDataSanitizer.Sanitize(content, identifiers)
                        });

                    }
                    catch (Exception ex)
                    {
                        throw new IOException($"Could not read {file.Name}. Overall feedback was cancelled to avoid grading incomplete input.", ex);
                    }
                }

                var requirements = OverallFeedbackPrompt.WithFiles(instructions, codeFiles);
                string feedback;

                switch (settings.Provider)
                {
                    case Models.LLMProvider.AzureOpenAI:
                        if (string.IsNullOrWhiteSpace(settings.AzureEndpoint) || 
                            string.IsNullOrWhiteSpace(settings.AzureApiKey) ||
                            string.IsNullOrWhiteSpace(settings.AzureDeployment))
                        {
                            MessageBox.Show("Azure OpenAI is not configured.\n\nPlease go to Tools ? LLM Configuration to set up your Azure credentials.", 
                                "Configuration Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        Debug.WriteLine($"Analyzing {checkedFiles.Count} file(s) with Azure OpenAI ({settings.AzureDeployment})...");
                        var azureService = new Services.AzureOpenAIService(
                            settings.AzureEndpoint, 
                            settings.AzureApiKey, 
                            settings.AzureDeployment);

                        var azureFiles = codeFiles.Select(f => new Services.CodeFile
                        {
                            FileName = f.Name,
                            Content = f.Content
                        }).ToList();

                        feedback = await azureService.AnalyzeCodeAsync(requirements, azureFiles, wrapPrompt: false, jobTitle: "Overall feedback");
                        break;

                    case Models.LLMProvider.OpenAI:
                        MessageBox.Show("OpenAI integration coming soon!\n\nFor now, please use Ollama (local) or Azure OpenAI.", 
                            "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;

                    case Models.LLMProvider.Ollama:
                    default:
                        Debug.WriteLine($"Analyzing {checkedFiles.Count} file(s) with Ollama ({settings.SelectedModel})...");

                        var ollamaService = new Services.OllamaService(
                            baseUrl: settings.OllamaBaseUrl,
                            model: settings.SelectedModel
                        );

                        var isAvailable = await ollamaService.IsAvailableAsync();
                        if (!isAvailable)
                        {
                            var msg = $"Ollama is not running or model '{settings.SelectedModel}' is not installed.\n\n" +
                                      "To use Ollama:\n" +
                                      "1. Install Ollama from https://ollama.ai\n" +
                                      $"2. Run: ollama pull {settings.SelectedModel}\n" +
                                      "3. Ensure Ollama is running\n\n" +
                                      "Or configure a different provider in Tools ? LLM Configuration.";
                            MessageBox.Show(msg, "Ollama Not Available", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        feedback = await ollamaService.AnalyzeCodeAsync(codeFiles, requirements, wrapPrompt: false, jobTitle: "Overall feedback");
                        break;
                }

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
            var setupWindow = new Windows.AssignmentSetupWindow(_currentAssignment)
            {
                Owner = this
            };

            if (setupWindow.ShowDialog() == true)
            {
                SetReviewAssignment(setupWindow.Assignment);
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

        private void CommentLayer_ApproveRequested(object? sender, Models.SectionFeedback feedback)
        {
            feedback.ReviewStatus = Models.FeedbackReviewStatus.Approved;
            if (_selectedTabButton?.Tag is string filePath)
            {
                TrackCommentForFile(filePath, feedback, publishToDraft: false);
                PersistCommentsForFile(filePath);
                RestoreApprovedFeedback(filePath, new[] { feedback });
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

            if (_fileComments.TryGetValue(filePath, out var comments))
            {
                comments.RemoveAll(c =>
                    c.ReviewContext == feedback.ReviewContext &&
                    string.Equals(c.SectionName, feedback.SectionName, StringComparison.OrdinalIgnoreCase) &&
                    c.StartLine == feedback.StartLine &&
                    c.EndLine == feedback.EndLine);
            }

            PersistCommentsForFile(filePath);
        }

        private bool IsSelectedFile(string path)
        {
            var selected = _selectedTabButton?.Tag as string;
            return !string.IsNullOrWhiteSpace(selected) && string.Equals(selected, path, StringComparison.OrdinalIgnoreCase);
        }

        private void SelectTab(FileTab btn)
        {
            _selectedTabButton = btn;
            _activeFile.Value = btn.Tag;

            var path = (string)btn.Tag!;
            codeEditor.Text = File.ReadAllText(path);
            RefreshReviewSelection();
            SetEmptyState(false);
            UpdateViolationsStatus();
        }

        private void LoadCommentsForFile(string filePath)
        {
            if (!_fileComments.TryGetValue(filePath, out var comments))
            {
                comments = _commentPersistenceService.LoadComments(filePath);
                _fileComments[filePath] = comments;
            }

            RenderCommentsForFile(filePath);
            RestoreApprovedFeedback(filePath, comments);
        }

        private void RenderCommentsForFile(string filePath)
        {
            // Async results may finish after this tab has been closed or replaced.
            if (!IsSelectedFile(filePath)) return;
            _commentLayer?.ClearComments();

            if (!_fileComments.TryGetValue(filePath, out var comments))
                return;

            foreach (var comment in comments.Where(c => MatchesCurrentReview(c) && c.ReviewStatus != Models.FeedbackReviewStatus.Rejected))
            {
                _commentLayer?.AddComment(comment, comment.StartLine, comment.EndLine);
            }
        }

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

        private void TrackCommentForFile(string filePath, Models.SectionFeedback feedback, string? draftTarget = null, bool publishToDraft = true)
        {
            if (string.IsNullOrWhiteSpace(filePath) || feedback == null)
                return;

            if (!_fileComments.TryGetValue(filePath, out var comments))
            {
                comments = _commentPersistenceService.LoadComments(filePath);
                _fileComments[filePath] = comments;
            }

            if (publishToDraft) feedback.ReviewContext = draftTarget ?? CurrentFeedbackKey(filePath);
            var existingIndex = comments.FindIndex(c =>
                c.ReviewContext == feedback.ReviewContext && c.IsOverallReview == feedback.IsOverallReview &&
                string.Equals(c.SectionName, feedback.SectionName, StringComparison.OrdinalIgnoreCase) &&
                c.StartLine == feedback.StartLine &&
                c.EndLine == feedback.EndLine);

            if (existingIndex >= 0)
            {
                comments[existingIndex] = feedback;
            }
            else
            {
                comments.Add(feedback);
            }
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

            if (_fileComments.TryGetValue(filePath, out var comments))
            {
                _commentPersistenceService.SaveComments(filePath, comments);
            }
            // An absent cache entry means this file was never loaded, not that
            // its persisted reviews should be deleted. Clear review is explicit.
        }

        // --- Folder / Student Loading -----------------------------------------

        private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            var result = dialog.ShowDialog();

            if (result != System.Windows.Forms.DialogResult.OK) return;

            // Store the opened directory path for violations config loading
            _openedDirectoryPath = dialog.SelectedPath;

            Debug.WriteLine($"Loading students from: {_openedDirectoryPath}");

            // Populate students list
            var students = Student.GetStudentsFromFolders(_openedDirectoryPath);
            listBoxStudents.Items.Clear();

            foreach (var student in students)
            {
                Debug.WriteLine($"Adding student: {student.FullName} | Folder: {student.Folder}");
                listBoxStudents.Items.Add(student);
            }

            // Clear tree view until a student is selected
            fileTreeView.Items.Clear();
        }

        private void ListBoxStudents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearFileTabs();
            if (listBoxStudents.SelectedItem is not Student student) return;
            CloseSidePanel();
            var path = student.Folder;
            if (path == null) return;

            Debug.WriteLine($"Student selected: {student.FullName}, Path: {path}");

            // Populate tree view with selected student's directory structure
            PopulateTreeView(path);
            OpenSavedReviewForSelectedStudent();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new Windows.SettingsWindow(_openedDirectoryPath)
            {
                Owner = this
            };

            if (settingsWindow.ShowDialog() == true && settingsWindow.SettingsSaved)
            {
                _panelPreferences = Services.WorkspacePanelPreferences.Load();
                ApplyPanelPreferences();
                // Refresh violations status with new settings
                UpdateViolationsStatus();
            }
        }

        private void LLMSettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var llmSettingsWindow = new Windows.LLMSettingsWindow
            {
                Owner = this
            };
            llmSettingsWindow.ShowDialog();
        }

        // --- File Tabs --------------------------------------------------------

        private void ClearFileTabs()
        {
            foreach (var tab in _fileTabs)
            {
                if (tab.Tag is string tabPath)
                {
                    PersistCommentsForFile(tabPath);
                }
            }

            _fileTabs.Clear();
            _activeFile.Value = "";
            _selectedTabButton = null;
            codeEditor.Text = string.Empty;
            _violationHighlighter.Clear();
            _gradingView.Clear();
            RefreshGradeSelection();
            SetEmptyState(true);
        }

        private void PopulateFileTabs(string name, string path, List<string> exclusions)
        {
            // Search for log files with priority: .fslog > .log > hdkvkt.txt
            Debug.WriteLine($"Searching for log files in: {path}");

            var resultsPath = FileHandler.SearchFile(path, "*.fslog");
            if (string.IsNullOrEmpty(resultsPath))
            {
                Debug.WriteLine("  .fslog not found, trying .log...");
                resultsPath = FileHandler.SearchFile(path, "*.log");
            }
            if (string.IsNullOrEmpty(resultsPath))
            {
                Debug.WriteLine("  .log not found, trying hdkvkt.txt...");
                resultsPath = FileHandler.SearchFile(path, "output.txt");
            }

            Debug.WriteLine($"Found log file: {(string.IsNullOrEmpty(resultsPath) ? "(none)" : resultsPath)}");

            if (name.StartsWith("Lab"))
            {
                exclusions.Add(name + ".cpp");
                exclusions.Add(name.Replace(" ", "") + ".cpp");
                SetResultStatusLabels(resultsPath, name);
            }

            var filePaths = FileHandler.SearchHeaderFiles(path, exclusions);
            var first = true;

            foreach (var fp in filePaths)
            {
                var btn = CreateFileTab(fp, first);
                _fileTabs.Add(btn);

                if (first)
                {
                    SelectTab(btn);
                    first = false;
                }
            }

            // Count violations across all loaded files
            UpdateViolationsStatus();
        }

        private void SetEmptyState(bool isEmpty)
        {
            if (isEmpty) _commentLayer?.ClearComments();
            emptyStateOverlay.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PopulateTreeView(string rootPath)
        {
            fileTreeView.Items.Clear();

            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                Debug.WriteLine($"Invalid root path: {rootPath}");
                return;
            }

            try
            {
                var rootItem = new Models.FileSystemItem(rootPath, true)
                {
                    IsExpanded = true
                };

                LoadDirectory(rootItem, rootPath);
                fileTreeView.Items.Add(rootItem);

                Debug.WriteLine($"Tree populated with root: {rootPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error populating tree: {ex.Message}");
                MessageBox.Show($"Error loading directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDirectory(Models.FileSystemItem parentItem, string directoryPath)
        {
            try
            {
                // Add subdirectories
                var directories = Directory.GetDirectories(directoryPath);
                foreach (var dir in directories.OrderBy(d => Path.GetFileName(d)))
                {
                    var dirItem = new Models.FileSystemItem(dir, true);
                    parentItem.Children.Add(dirItem);

                    // Recursively load subdirectories
                    LoadDirectory(dirItem, dir);
                }

                // Add files
                var files = Directory.GetFiles(directoryPath);
                foreach (var file in files.OrderBy(f => Path.GetFileName(f)))
                {
                    var fileItem = new Models.FileSystemItem(file, false);
                    parentItem.Children.Add(fileItem);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Access denied to: {directoryPath} - {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading directory {directoryPath}: {ex.Message}");
            }
        }

        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is not Models.FileSystemItem selectedItem)
                return;

            Debug.WriteLine($"Tree item selected: {selectedItem.FullPath} (IsDirectory: {selectedItem.IsDirectory})");

            // Only open files, not directories
            // Directories just expand/collapse in the tree
            // Solution files are launched from the right-click menu instead of the editor.
            if (!selectedItem.IsDirectory && !selectedItem.IsSolution)
            {
                // Recycling can restore selection without changing the active file.
                // Reopening it would reset the retained editor's document and caret.
                if (_selectedTabButton?.Tag is string activePath &&
                    string.Equals(activePath, selectedItem.FullPath, StringComparison.OrdinalIgnoreCase)) return;
                OpenFileInTab(selectedItem.FullPath);
            }
        }

        private void OpenFileInTab(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"File not found: {filePath}");
                return;
            }

            try
            {
                // Check if file is already open
                foreach (var tab in _fileTabs)
                {
                    if (tab.Tag is string existingPath && existingPath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectTab(tab);
                        Debug.WriteLine($"File already open, switching to tab: {filePath}");
                        return;
                    }
                }

                var isFirstTab = _fileTabs.Count == 0;
                var tabButton = CreateFileTab(filePath, isFirstTab);
                _fileTabs.Add(tabButton);
                SelectTab(tabButton);

                if (_fileTabs.Count == 1)
                {
                    SetEmptyState(false);
                }

                Debug.WriteLine($"Opened file in new tab: {filePath}");

                // Update violations for this file
                UpdateViolationsStatus();

                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening file {filePath}: {ex.Message}");
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenTreeItemInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (fileTreeView.SelectedItem is not Models.FileSystemItem selectedItem)
                return;

            try
            {
                var arguments = selectedItem.IsDirectory
                    ? $"\"{selectedItem.FullPath}\""
                    : $"/select,\"{selectedItem.FullPath}\"";
                Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening in explorer: {ex.Message}");
                MessageBox.Show($"Error opening in File Explorer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private readonly Services.ReviewGeneration _reviewGeneration = new();

        private void ClearReviewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (fileTreeView.SelectedItem is not Models.FileSystemItem item || item.IsDirectory)
                return;
            try
            {
                // Delete first: a storage failure must not leave a falsely cleared UI.
                _commentPersistenceService.DeleteComments(item.FullPath);
                _reviewGeneration.Clear(item.FullPath);
                _fileComments.Remove(item.FullPath);
                if (IsSelectedFile(item.FullPath))
                    _commentLayer?.ClearComments();
                Title = $"GuidedGrade - Review cleared for {item.Name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not clear the review: {ex.Message}", "Clear review", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshTreeView_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_openedDirectoryPath))
            {
                PopulateTreeView(_openedDirectoryPath);
            }
        }

        private void FileTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = FindVisualParent<System.Windows.Controls.TreeViewItem>(e.OriginalSource as DependencyObject);
            if (treeViewItem == null)
                return;

            treeViewItem.IsSelected = true;
            treeViewItem.Focus();
        }

        private void FileTreeContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu)
                return;

            var isSolution = fileTreeView.SelectedItem is Models.FileSystemItem item && item.IsSolution;
            var visibility = isSolution ? Visibility.Visible : Visibility.Collapsed;

            foreach (var obj in menu.Items)
            {
                if (obj is FrameworkElement element && Equals(element.Tag, "solution-command"))
                    element.Visibility = visibility;
                if (obj is FrameworkElement fileCommand && Equals(fileCommand.Tag, "file-command"))
                    fileCommand.Visibility = fileTreeView.SelectedItem is Models.FileSystemItem selected && !selected.IsDirectory
                        ? Visibility.Visible : Visibility.Collapsed;
            }
        }

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
            var assignment = _currentAssignment == null ? null : new GradingAssignment
            {
                Course = _currentAssignment.Course,
                Title = _currentAssignment.Title,
                Requirements = _currentAssignment.Requirements,
                Rubric = _currentAssignment.Rubric.Select(r => new RubricItem(r.Name, r.MaxPoints)).ToList()
            };
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

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T match)
                    return match;
                child = VisualTreeHelper.GetParent(child);
            }

            return null;
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
            var fileText = await File.ReadAllTextAsync(filePath);
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

        private List<Models.FileSystemItem> GetCheckedFiles(System.Collections.IEnumerable items)
        {
            var result = new List<Models.FileSystemItem>();

            foreach (var item in items)
            {
                if (item is Models.FileSystemItem fsItem)
                {
                    // Add if it's a checked file
                    if (!fsItem.IsDirectory && fsItem.IsCheckedForAnalysis)
                    {
                        result.Add(fsItem);
                    }
                    if (fsItem.Children != null)
                    {
                        result.AddRange(GetCheckedFiles(fsItem.Children));
                    }
                }
            }

            return result;
        }

        private void PopulateFileTabsWithDirectories(string path)
        {
            Debug.WriteLine($"=== PopulateFileTabsWithDirectories called with path: {path}");

            // Search for log files and parse build/score info
            Debug.WriteLine("Searching for log files (.fslog, .log, output.txt)...");
            var resultsPath = FileHandler.SearchFile(path, "*.fslog");

            if (string.IsNullOrEmpty(resultsPath))
            {
                Debug.WriteLine("  output.txt not found, trying hdkvkt.txt...");
                resultsPath = FileHandler.SearchFile(path, "hdkvkt.txt");
            }

            Debug.WriteLine($"Found log file: {(string.IsNullOrEmpty(resultsPath) ? "(none)" : resultsPath)}");

            // Parse and set build/score from log file
            SetResultStatusLabels(resultsPath, Path.GetFileName(path));

            var exclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "DONOTUSEANYTHINGINTHISFILE.h", "Source.h", "Helper.cpp", "Helper.h",
                "Source.cpp", "Test.cpp", "Test.h", "Tester.cpp", "Tester.h",
                "Utility.cpp", "Utility.h", "UI.h", "ShopUtils.cpp", "ShopUtils.h",
                "LabUI.h", "resource.h", "LLMChecker.h", "ProgressBar.h", "Result.h",
                "Results.h", "ResultsLib.h", "LabTestUtils.h", "Console.h", "Console.cpp",
                ".vs", "bin", "obj", "Debug", "Release", "x64", "x86"
            };

            // Get all files recursively from the directory
            var allFiles = new List<string>();
            try
            {
                var allFoundFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                Debug.WriteLine($"Total files found: {allFoundFiles.Length}");

                allFiles = allFoundFiles
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f).ToLower();
                        var fileName = Path.GetFileName(f);
                        var pathParts = f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                        // Check if file is in excluded directories
                        bool inExcludedDir = pathParts.Any(p => exclusions.Contains(p));

                        // Only include source code files
                        bool isSourceFile = ext == ".cpp" || ext == ".h" || ext == ".hpp" || 
                                          ext == ".c" || ext == ".cs" || ext == ".java" ||
                                          ext == ".py" || ext == ".js" || ext == ".ts";

                        return isSourceFile && !exclusions.Contains(fileName) && !inExcludedDir;
                    })
                    .OrderBy(f => f)
                    .ToList();

                Debug.WriteLine($"Files after filtering: {allFiles.Count}");
                foreach (var file in allFiles.Take(10))
                {
                    Debug.WriteLine($"  - {file}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading files: {ex.Message}");
                SetEmptyState(true);
                return;
            }

            if (!allFiles.Any())
            {
                Debug.WriteLine("No files found after filtering - showing empty state");
                SetEmptyState(true);
                return;
            }

            Debug.WriteLine($"Creating {allFiles.Count} file tabs");
            var first = true;
            foreach (var filePath in allFiles)
            {
                var btn = CreateFileTab(filePath, first);
                _fileTabs.Add(btn);
                Debug.WriteLine($"Added tab for: {Path.GetFileName(filePath)}");

                if (first)
                {
                    SelectTab(btn);
                    first = false;
                }
            }

            UpdateViolationsStatus();
        }

        private static FileTab CreateFileTab(string filePath, bool first = false) => new(filePath);

        private void CloseFileTab(string filePath)
        {
            PersistCommentsForFile(filePath);

            var matchingTab = _fileTabs
                .FirstOrDefault(tab => tab.Tag is string existingPath && existingPath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

            if (matchingTab != null)
            {
                _fileTabs.Remove(matchingTab);
            }

            if (_selectedTabButton != null && _selectedTabButton.Tag is string selectedPath &&
                string.Equals(selectedPath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                _selectedTabButton = null;
                if (_fileTabs.Count > 0)
                {
                    var nextTab = _fileTabs[0];
                    SelectTab(nextTab);
                }
                else
                {
                    codeEditor.Text = string.Empty;
                    _violationHighlighter.Clear();
                    SetEmptyState(true);
                }
            }

            if (_fileTabs.Count == 0)
            {
                _selectedTabButton = null;
                _activeFile.Value = "";
                codeEditor.Text = string.Empty;
                _violationHighlighter.Clear();
                SetEmptyState(true);
            }
            RefreshGradeSelection();
        }

        // --- Status Bar -------------------------------------------------------

        private void UpdateViolationsStatus()
        {
            Debug.WriteLine("=== UpdateViolationsStatus() CALLED ===");

            // Load violations from config file or use defaults
            var configService = new ViolationsConfigService();
            var violationTerms = configService.LoadViolations(_openedDirectoryPath);

            Debug.WriteLine($"Opened Directory Path: {_openedDirectoryPath ?? "(null)"}");
            Debug.WriteLine($"Loaded {violationTerms.Count} violation terms: {string.Join(", ", violationTerms)}");
            Debug.WriteLine($"File patterns: {string.Join(", ", configService.FilePatterns)}");

            var matcher = new ViolationsMatcher(violationTerms);

            // Collect text from all loaded tabs, not just the visible one
            var allViolations = new List<Violation>();
            Debug.WriteLine($"Scanning {_fileTabs.Count} file tabs...");

            foreach (var tab in _fileTabs)
            {
                var path = (string)tab.Tag!;
                Debug.WriteLine($"  Checking file: {path}");

                if (!File.Exists(path))
                {
                    Debug.WriteLine($"    SKIPPED: File does not exist");
                    continue;
                }

                // Only scan files that match the configured patterns
                if (!configService.ShouldScanFile(path))
                {
                    Debug.WriteLine($"    SKIPPED: File does not match configured patterns");
                    continue;
                }

                Debug.WriteLine($"    SCANNING: File matches pattern");

                var text = File.ReadAllText(path);
                Debug.WriteLine($"    File length: {text.Length} characters");

                var violations = matcher.GetViolations(text);
                Debug.WriteLine($"    Found {violations.Count} violations in this file");

                // Tag each violation with its source file for the tooltip
                foreach (var v in violations)
                {
                    Debug.WriteLine($"      - Line {v.LineNumber}: '{v.MatchedText}' {v.Context}");
                    allViolations.Add(new Violation(
                        v.LineNumber,
                        v.MatchedText,
                        v.Context,
                        $"{Path.GetFileName(path)} — {v.LineContent}"));
                }
            }

            Debug.WriteLine($"TOTAL VIOLATIONS FOUND: {allViolations.Count}");

            // Highlight violations only in the currently visible file
            var visibleViolations = allViolations
                .Where(v => v.LineContent.Contains(
                    _selectedTabButton != null
                        ? Path.GetFileName((string)_selectedTabButton.Tag!)
                        : string.Empty))
                .ToList();

            Debug.WriteLine($"Visible violations (for current tab): {visibleViolations.Count}");

            // Actually re-run for visible file to get correct line numbers
            if (_selectedTabButton != null)
            {
                var visiblePath = (string)_selectedTabButton.Tag!;
                Debug.WriteLine($"Selected tab: {visiblePath}");

                // Only highlight if the visible file should be scanned
                if (configService.ShouldScanFile(visiblePath))
                {
                    var visibleText = File.ReadAllText(visiblePath);
                    var visibleFileViolations = matcher.GetViolations(visibleText);
                    var lineNumbers = visibleFileViolations.Select(v => v.LineNumber).ToList();
                    Debug.WriteLine($"Highlighting lines: {string.Join(", ", lineNumbers)}");
                    _violationHighlighter.SetViolationLines(lineNumbers);
                }
                else
                {
                    Debug.WriteLine("Visible file does not match scan patterns - clearing highlights");
                    _violationHighlighter.Clear();
                }
            }
            else
            {
                Debug.WriteLine("No selected tab - clearing highlights");
                _violationHighlighter.Clear();
            }

            _violationCount.Value = allViolations.Count.ToString();
            Debug.WriteLine($"Status bar updated to: {allViolations.Count}");

            if (allViolations.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Found {allViolations.Count} violation(s):\n");

                // Group by file for readability
                var byFile = allViolations
                    .GroupBy(v => v.LineContent.Split('—')[0].Trim());

                foreach (var group in byFile)
                {
                    sb.AppendLine($"-- {group.Key}");
                    foreach (var v in group)
                        sb.AppendLine($"  Line {v.LineNumber}: '{v.MatchedText}'  {v.Context}");
                    sb.AppendLine();
                }

                _violationTooltip.Content = new WpfTextBlock
                    {
                        Text = sb.ToString().TrimEnd(),
                        FontFamily = new FontFamily("Consolas"),
                        MaxWidth = 600,
                        TextWrapping = TextWrapping.Wrap
                };
            }
            else
            {
                _violationTooltip.Content = null;
            }

            _violationColor.Value = allViolations.Count switch
            {
                > 3 => "#FF6666",
                > 0 => "#FFA500",
                _ => "#CCCCCC"
            };

            // Update violations list panel
            violationsList.ItemsSource = allViolations;
            Debug.WriteLine($"Violations list updated with {allViolations.Count} items");
            Debug.WriteLine($"Violations panel visibility: {violationsPanel.Visibility}");
            Debug.WriteLine("=== UpdateViolationsStatus() COMPLETE ===\n");
        }

        private void StatusViolations_Click(object sender, MouseButtonEventArgs e)
            => SelectToolsPanel(true, toggleSelected: true);

        private void OpenRuntimeTerminalPanel() => SelectToolsPanel(false);

        private void AppendToRuntimeTerminal(string text, Brush? brush = null)
            => _runtimeTerminal.Append(text, brush ?? Brushes.LightGray);

        private void ViolationsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (violationsList.SelectedItem is Violation violation)
            {
                // Navigate to the line in the editor
                try
                {
                    var line = codeEditor.Document.GetLineByNumber(violation.LineNumber);
                    codeEditor.ScrollToLine(violation.LineNumber);
                    codeEditor.Select(line.Offset, line.Length);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error navigating to line: {ex.Message}");
                }
            }
        }

        // TODO: Remove?
        private List<int> GetViolationLineNumbers(List<string> violations)
        {
            var lines = new HashSet<int>();
            var docLines = codeEditor.Text.Split('\n');

            for (int i = 0; i < docLines.Length; i++)
            {
                var line = docLines[i];
                if (violations.Any(v => line.Contains(v)))
                    lines.Add(i + 1); // AvalonEdit lines are 1-indexed
            }

            return lines.ToList();
        }

        private void SetResultStatusLabels(string resultsPath, string projectName)
        {
            Debug.WriteLine($"=== SetResultStatusLabels called ===");
            Debug.WriteLine($"  Results path: {resultsPath}");
            Debug.WriteLine($"  Project name: {projectName}");

            if (!string.IsNullOrEmpty(resultsPath) && File.Exists(resultsPath))
            {
                var results = FileHandler.ParseFile(resultsPath);
                Debug.WriteLine($"  Parsed {results.Count} results from log file");

                if (results.Count > 0)
                {
                    // Determine file type for proper parsing
                    var fileName = Path.GetFileName(resultsPath).ToLower();

                    if (fileName.EndsWith(".fslog") || fileName.EndsWith(".log") || fileName == "output.txt")
                    {
                        // For .fslog/.log/output.txt: First result is build count, second (if exists) is score
                        _buildCount.Value = results[0].Number.ToString();
                        Debug.WriteLine($"  Builds: {results[0].Number}");

                        if (results.Count > 1)
                        {
                            _score.Value = results[1].Number.ToString();
                            Debug.WriteLine($"  Score: {results[1].Number}");
                        }
                        else
                        {
                            _score.Value = "N/A";
                            Debug.WriteLine($"  Score: N/A (not found in log)");
                        }
                    }
                    else
                    {
                        // For hdkvkt.txt or other formats: results.Count is build count, last result is score
                        _buildCount.Value = results.Count.ToString();
                        Debug.WriteLine($"  Builds: {results.Count}");

                        _score.Value = results[^1].Number.ToString();
                        Debug.WriteLine($"  Score: {results[^1].Number}");
                    }
                }
                else
                {
                    Debug.WriteLine("  No results parsed from file");
                    _buildCount.Value = "0";
                    _score.Value = "N/A";
                }
            }
            else
            {
                Debug.WriteLine($"  Log file not found or empty path");
                Debug.WriteLine($"  Searched for: *.fslog, *.log in {Path.GetDirectoryName(resultsPath) ?? "(unknown)"}");

                _buildCount.Value = "0";
                _score.Value = "N/A";
            }

            Debug.WriteLine($"=== SetResultStatusLabels complete ===\n");
        }

        // --- Context Menu (Students ListBox) ----------------------------------

        // NOTE: This method is no longer used - tree view has its own context menu
        /*
        private void OpenInFileExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxStudents.SelectedItem is not Student student) return;

            if (!string.IsNullOrEmpty(student.Folder) && Directory.Exists(student.Folder))
                Process.Start("explorer.exe", student.Folder);
            else
                System.Windows.MessageBox.Show(
                    "Invalid file path or file does not exist.", "Error");
        }
        */

        // NOTE: This method is no longer used - tree view doesn't have student-specific copy
        /*
        private void CopyStudentNameAndNumber_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxStudents.SelectedItem is not Student student) return;
            Clipboard.SetText($"{student.FirstName} {student.LastName}\t{student.IdNumber}");
        }
        */

        private void ListBoxItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item)
            {
                item.IsSelected = true;
                e.Handled = false;
            }
        }

        // --- Context Menu (Assignments ListBox) ----------------------------------

        // NOTE: This method is no longer used - tree view has its own context menu
        /*
        private void OpenAssignmentInFileExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxAssignments.SelectedItem is not Assignment assignment) return;

            if (!string.IsNullOrEmpty(assignment.Folder) && Directory.Exists(assignment.Folder))
                Process.Start("explorer.exe", assignment.Folder);
            else
                System.Windows.MessageBox.Show(
                    "Invalid folder path or folder does not exist.", "Error");
        }
        */


        // --- Open in New Window -----------------------------------------------

        private void OpenNewWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // new FormCodeView(codeEditor.Text, "test.cpp").Show();
        }

        private void OpenSidePanel()
        {
            sidePanelSplitter.Visibility = Visibility.Visible;
            sidePanelColumn.Width = new GridLength(Math.Min(_sidePanelWidth, Math.Max(300, ActualWidth * 0.45)));
            UpdatePinnedTabs();
        }

        private void CloseSidePanel()
        {
            // Save current width before closing
            _sidePanelWidth = sidePanelColumn.Width.Value > 0
                ? sidePanelColumn.Width.Value
                : _sidePanelWidth;

            sidePanelSplitter.Visibility = Visibility.Collapsed;
            sidePanelColumn.Width = new GridLength(0);
            UpdatePinnedTabs();
        }

        //private void ButtonOpenGrading_Click(object sender, RoutedEventArgs e)
        //{
        //    if (tabGrading.IsChecked == true)
        //    {
        //        tabGrading.IsChecked = false;
        //    }
        //    else
        //    {
        //        tabGrading.IsChecked = true;
        //    }
        //}

            }
        }
