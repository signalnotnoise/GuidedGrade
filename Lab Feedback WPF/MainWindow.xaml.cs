using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.Services;
using Lab_Feedback_WPF.Views;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;
//using WpfButton = System.Windows.Controls.Button;
//using WpfTextBox = System.Windows.Controls.TextBox;
//using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfButton = Wpf.Ui.Controls.Button;
using WpfTextBox = Wpf.Ui.Controls.TextBox;
using WpfTextBlock = Wpf.Ui.Controls.TextBlock;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;


namespace Lab_Feedback_WPF
{
    public partial class MainWindow : FluentWindow
    {
        private readonly GradingView _gradingView = new();
        private readonly AiTestQueue _aiTestQueue = new();
        private readonly RuntimeTerminalPresenter _runtimeTerminal;
        private readonly Services.AssignmentPersistenceService _assignmentPersistenceService = new();

        private WpfButton? _selectedTabButton;
        private ExtractionProgressDialog? _progressDialog;
        private ViolationHighlighter _violationHighlighter = null!;
        private double _sidePanelWidth = 500;
        private string? _openedDirectoryPath;

        // Section grading
        private Models.GradingAssignment? _currentAssignment;
        private Controls.InlineCommentLayer? _commentLayer;
        private readonly Services.CommentPersistenceService _commentPersistenceService = new();
        private readonly Dictionary<string, List<Models.SectionFeedback>> _fileComments = new(StringComparer.OrdinalIgnoreCase);


        public MainWindow()
        {
            InitializeComponent();
            _runtimeTerminal = new RuntimeTerminalPresenter(runtimeTerminalRichTextBox);
            Closed += (_, _) => _runtimeTerminal.Dispose();
            LoadMonokaiTheme();
            SetupInlineComments();
            LoadSavedCourseOptions();
        }

        private void LoadSavedCourseOptions()
        {
            var courses = _assignmentPersistenceService.GetCourseNames();
            cmbSavedCourses.ItemsSource = courses;

            if (courses.Count > 0)
            {
                cmbSavedCourses.SelectedIndex = 0;
            }
        }

        private void LoadSavedAssignmentsForCourse(string course)
        {
            cmbSavedAssignments.Items.Clear();
            if (string.IsNullOrWhiteSpace(course))
                return;

            var assignments = _assignmentPersistenceService.GetAssignmentsByCourse(course);
            foreach (var assignment in assignments)
            {
                cmbSavedAssignments.Items.Add(assignment.Title);
            }

            if (cmbSavedAssignments.Items.Count > 0)
            {
                cmbSavedAssignments.SelectedIndex = 0;
            }
        }

        private void SavedCourse_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSavedCourses.SelectedItem is not string selectedCourse)
                return;

            LoadSavedAssignmentsForCourse(selectedCourse);
        }

        private void SavedAssignment_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSavedAssignments.SelectedItem is not string selectedTitle)
                return;

            var course = cmbSavedCourses.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(course))
                return;

            var assignment = _assignmentPersistenceService.LoadAssignment(course, selectedTitle);
            if (assignment == null)
                return;

            _currentAssignment = assignment;
            CloseSidePanel();
            MessageBox.Show($"Loaded assignment '{assignment.Title}' for course '{assignment.Course}'.",
                "Saved Assignment Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // --- Theme ------------------------------------------------------------

        private void LoadMonokaiTheme()
        {
            _violationHighlighter = new ViolationHighlighter(codeEditor);

            var uri = new Uri("pack://application:,,,/Resources/Monokai.xshd");
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

        private async Task GradeFileSectionsAsync(string filePath, bool clearComments = true, string? capturedTarget = null, GradingAssignment? capturedAssignment = null)
        {
            var draftTarget = capturedTarget ?? CurrentFeedbackKey();
            var assignment = capturedAssignment ?? _currentAssignment;
            if (assignment == null || assignment.Rubric.Count == 0)
                return;

            var fileText = await File.ReadAllTextAsync(filePath);
            var sections = ExtractCodeSections(fileText, Path.GetFileNameWithoutExtension(filePath));

            if (sections.Count == 0)
            {
                var singleSection = new CodeSection
                {
                    Name = Path.GetFileName(filePath),
                    Code = fileText,
                    StartLine = 1,
                    EndLine = fileText.Split('\n').Length
                };
                sections.Add(singleSection);
            }

            if (clearComments)
            {
                _commentLayer?.ClearComments();
            }

            var gradingService = new Services.SectionGradingService(assignment);
            var relatedFiles = GetRelatedFilesForGrading(filePath);
            foreach (var section in sections)
            {
                var feedback = await gradingService.AnalyzeSectionAsync(
                    section.Name,
                    section.Code,
                    assignment.Rubric,
                    GetStudentIdentifiers(filePath),
                    relatedFiles);

                feedback.StartLine = section.StartLine;
                feedback.EndLine = section.EndLine;
                TrackCommentForFile(filePath, feedback, draftTarget);
                if (draftTarget == CurrentFeedbackKey() && IsSelectedFile(filePath))
                    _commentLayer?.AddComment(feedback, section.StartLine, section.EndLine);
            }

            PersistCommentsForFile(filePath);

            if (sections.Count > 0)
            {
                codeEditor.ScrollToLine(Math.Max(1, sections[0].StartLine));
            }
        }

        private async Task GradeSelectedFilesWithSectionsAsync(List<Models.FileSystemItem> checkedFiles)
        {
            if (checkedFiles.Count == 0)
                return;

            var target = CurrentFeedbackKey();
            var assignment = _currentAssignment;
            _commentLayer?.ClearComments();

            foreach (var file in checkedFiles)
            {
                await GradeFileSectionsAsync(file.FullPath, clearComments: false, capturedTarget: target, capturedAssignment: assignment);
            }
        }

        private List<CodeSection> ExtractCodeSections(string fileText, string defaultName)
        {
            var sections = new List<CodeSection>();
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

        private void CreateCommentOverlay()
        {
            _commentLayer = new Controls.InlineCommentLayer(codeEditor);
            _commentLayer.ApproveRequested += CommentLayer_ApproveRequested;
            _commentLayer.RegenerateRequested += CommentLayer_RegenerateRequested;
            _commentLayer.RejectRequested += CommentLayer_RejectRequested;

            if (commentOverlay != null)
            {
                commentOverlay.Children.Add(_commentLayer);
                Panel.SetZIndex(_commentLayer, 10);
            }
        }

        private void AddCommentToCurrentFile(Models.SectionFeedback feedback)
        {
            if (_commentLayer == null)
                return;

            var startLine = codeEditor.Document.GetLineByOffset(codeEditor.SelectionStart).LineNumber;
            var endLine = codeEditor.Document.GetLineByOffset(codeEditor.SelectionStart + codeEditor.SelectionLength).LineNumber;
            feedback.StartLine = startLine;
            feedback.EndLine = endLine;

            if (_selectedTabButton?.Tag is string filePath)
            {
                TrackCommentForFile(filePath, feedback);
                PersistCommentsForFile(filePath);
            }

            _commentLayer.AddComment(feedback, startLine, endLine);
        }

        private void GradeCurrentFileWithSections()
        {
            var draftTarget = CurrentFeedbackKey();
            var assignment = _currentAssignment;
            if (assignment == null || assignment.Rubric.Count == 0)
                return;

            var fileText = codeEditor.Text;
            var sections = ExtractCodeSections(fileText, Path.GetFileNameWithoutExtension((string?)_selectedTabButton?.Tag ?? "file"));

            if (sections.Count == 0)
            {
                sections.Add(new CodeSection
                {
                    Name = "Whole File",
                    Code = fileText,
                    StartLine = 1,
                    EndLine = fileText.Split('\n').Length
                });
            }

            _commentLayer?.ClearComments();

            var filePath = (string?)_selectedTabButton?.Tag ?? string.Empty;
            var relatedFiles = GetRelatedFilesForGrading(filePath);
            foreach (var section in sections)
            {
                _ = Task.Run(async () =>
                {
                    var gradingService = new Services.SectionGradingService(assignment);
                    var feedback = await gradingService.AnalyzeSectionAsync(
                        section.Name,
                        section.Code,
                        assignment.Rubric,
                        GetStudentIdentifiers(filePath),
                        relatedFiles);

                    feedback.StartLine = section.StartLine;
                    feedback.EndLine = section.EndLine;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (!string.IsNullOrWhiteSpace(filePath))
                        {
                            TrackCommentForFile(filePath, feedback, draftTarget);
                        }

                        if (string.IsNullOrWhiteSpace(filePath) || IsSelectedFile(filePath))
                            _commentLayer?.AddComment(feedback, section.StartLine, section.EndLine);

                        if (!string.IsNullOrWhiteSpace(filePath))
                        {
                            PersistCommentsForFile(filePath);
                        }
                    });
                });
            }
        }

        private async Task GradeCurrentFileWithSectionsAsync()
        {
            var draftTarget = CurrentFeedbackKey();
            var assignment = _currentAssignment;
            if (assignment == null || assignment.Rubric.Count == 0)
                return;

            var fileText = codeEditor.Text;
            var sections = ExtractCodeSections(fileText, Path.GetFileNameWithoutExtension((string?)_selectedTabButton?.Tag ?? "file"));

            if (sections.Count == 0)
            {
                sections.Add(new CodeSection
                {
                    Name = "Whole File",
                    Code = fileText,
                    StartLine = 1,
                    EndLine = fileText.Split('\n').Length
                });
            }

            _commentLayer?.ClearComments();

            var filePath = (string?)_selectedTabButton?.Tag ?? string.Empty;
            var gradingService = new Services.SectionGradingService(assignment);
            var relatedFiles = GetRelatedFilesForGrading(filePath);
            foreach (var section in sections)
            {
                var feedback = await gradingService.AnalyzeSectionAsync(
                    section.Name,
                    section.Code,
                    assignment.Rubric,
                    GetStudentIdentifiers(filePath),
                    relatedFiles);

                feedback.StartLine = section.StartLine;
                feedback.EndLine = section.EndLine;
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    TrackCommentForFile(filePath, feedback, draftTarget);
                }

                if (string.IsNullOrWhiteSpace(filePath) || IsSelectedFile(filePath))
                    _commentLayer?.AddComment(feedback, section.StartLine, section.EndLine);
            }

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                PersistCommentsForFile(filePath);
            }
        }

        private void AddCommentBeforeCurrentSection(Models.SectionFeedback feedback)
        {
            if (_commentLayer == null)
                return;

            var lineNumber = codeEditor.Document.GetLineByOffset(codeEditor.SelectionStart).LineNumber;
            feedback.StartLine = lineNumber;
            feedback.EndLine = lineNumber;

            if (_selectedTabButton?.Tag is string filePath)
            {
                TrackCommentForFile(filePath, feedback);
                PersistCommentsForFile(filePath);
            }

            _commentLayer.AddComment(feedback, lineNumber, lineNumber);
        }

        private void AddCommentAtLine(Models.SectionFeedback feedback, int lineNumber)
        {
            feedback.StartLine = lineNumber;
            feedback.EndLine = lineNumber;

            if (_selectedTabButton?.Tag is string filePath)
            {
                TrackCommentForFile(filePath, feedback);
                PersistCommentsForFile(filePath);
            }

            _commentLayer?.AddComment(feedback, lineNumber, lineNumber);
        }

        private void AddCommentForSection(Models.SectionFeedback feedback, int startLine, int endLine)
        {
            feedback.StartLine = startLine;
            feedback.EndLine = endLine;

            if (_selectedTabButton?.Tag is string filePath)
            {
                TrackCommentForFile(filePath, feedback);
                PersistCommentsForFile(filePath);
            }

            _commentLayer?.AddComment(feedback, startLine, endLine);
        }

        private async void AnalyzeWithLLM_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var feedbackTarget = CurrentFeedbackKey();
                // Collect all checked files from the tree
                var checkedFiles = GetCheckedFiles(fileTreeView.Items);

                if (checkedFiles.Count == 0)
                {
                    MessageBox.Show("Please select files to analyze by checking the boxes next to them.", 
                        "No Files Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var settings = Models.LLMSettings.Load();


                if (_currentAssignment != null && _currentAssignment.Rubric.Count > 0)
                {
                    await GradeSelectedFilesWithSectionsAsync(checkedFiles);

                    MessageBox.Show($"Section-by-section grading complete for {checkedFiles.Count} file(s).",
                        "Grading Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // existing logic continues below
                var codeFiles = new List<Services.OllamaService.CodeFile>();
                foreach (var file in checkedFiles)
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(file.FullPath);
                        var identifiers = GetStudentIdentifiers(file.FullPath);
                        codeFiles.Add(new Services.OllamaService.CodeFile
                        {
                            Name = Services.StudentDataSanitizer.AnonymousFileName(codeFiles.Count + 1, Path.GetExtension(file.Name)),
                            Content = Services.StudentDataSanitizer.Sanitize(content, identifiers)
                        });

                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error reading {file.FullPath}: {ex.Message}");
                    }
                }

                var requirements = settings.RequirementsTemplate;
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

                        feedback = await azureService.AnalyzeCodeAsync(requirements, azureFiles);
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

                        MessageBox.Show(
                            $"Analyzing {checkedFiles.Count} file(s) with Ollama {settings.SelectedModel}...\n\n" +
                            "?? This may take 30-120 seconds\n" +
                            "? First run loads model into RAM\n" +
                            "?? Subsequent runs are faster\n\n" +
                            "Click OK to start (runs in background).",
                            "Analysis Starting",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        feedback = await ollamaService.AnalyzeCodeAsync(codeFiles, requirements);
                        break;
                }

                PresentGeneratedFeedback(feedbackTarget, feedback);
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
                _currentAssignment = setupWindow.Assignment;
                CloseSidePanel();
                MessageBox.Show($"Assignment '{_currentAssignment.Title}' configured with {_currentAssignment.Rubric.Count} rubric items.",
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

            MessageBox.Show($"Analyzing '{sectionName}'...\n\nThis may take 30-120 seconds.",
                "Analysis Started", MessageBoxButton.OK, MessageBoxImage.Information);

            try
            {
                var gradingService = new Services.SectionGradingService(_currentAssignment);
                var currentPath = _selectedTabButton?.Tag as string;
                var feedback = await gradingService.AnalyzeSectionAsync(
                    sectionName,
                    selection,
                    relevantItems,
                    GetStudentIdentifiers(currentPath),
                    GetRelatedFilesForGrading(currentPath));

                feedback.StartLine = startLine;
                feedback.EndLine = endLine;
                if (!string.IsNullOrWhiteSpace(currentPath))
                {
                    TrackCommentForFile(currentPath, feedback, draftTarget);
                    PersistCommentsForFile(currentPath);
                }

                if (draftTarget == CurrentFeedbackKey() && currentPath != null && IsSelectedFile(currentPath))
                    _commentLayer?.AddComment(feedback, startLine, endLine);
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

            try
            {
                var filePath = _selectedTabButton?.Tag as string;
                var sectionCode = GetSectionText(feedback);
                var gradingService = new Services.SectionGradingService(_currentAssignment);
                var regenerated = await gradingService.AnalyzeSectionAsync(
                    feedback.SectionName,
                    sectionCode,
                    _currentAssignment.Rubric,
                    GetStudentIdentifiers(filePath),
                    GetRelatedFilesForGrading(filePath));

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

        private void FileTab_Click(object sender, RoutedEventArgs e)
            => SelectTab((WpfButton)sender);


        private void SelectTab(WpfButton btn)
        {
            if (_selectedTabButton != null)
                _selectedTabButton.Background =
                    new SolidColorBrush(Color.FromArgb(255, 39, 40, 34));

            _selectedTabButton = btn;
            btn.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));

            var path = (string)btn.Tag!;
            codeEditor.Text = File.ReadAllText(path);
            LoadCommentsForFile(path);
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

            foreach (var comment in comments.Where(c => c.ReviewStatus != Models.FeedbackReviewStatus.Rejected))
            {
                _commentLayer?.AddComment(comment, comment.StartLine, comment.EndLine);
            }
        }

        private IReadOnlyList<string> GetStudentIdentifiers(string? filePath)
        {
            return Services.StudentDataSanitizer.GetIdentifiers(
                _gradingView.CurrentStudent,
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

            extraPaths.AddRange(panelFileTabs.Children
                .OfType<WpfButton>()
                .Select(tab => tab.Tag as string)
                .Where(path => !string.IsNullOrWhiteSpace(path))!);

            return Services.RelatedFileResolver.FindRelatedFiles(
                filePath,
                extraPaths,
                _gradingView.CurrentStudent?.Folder);
        }

        private Services.SubmissionExecutionService CreateExecutionService()
            => new Services.SubmissionExecutionService(confirmLocal: warning =>
                MessageBox.Show(this, warning, "Run student code locally?",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
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
                comments = new List<Models.SectionFeedback>();
                _fileComments[filePath] = comments;
            }

            var existingIndex = comments.FindIndex(c =>
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
            else
            {
                _commentPersistenceService.DeleteComments(filePath);
            }
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
            if (listBoxStudents.SelectedItem is not Student student) return;

            var path = student.Folder;
            if (path == null) return;

            Debug.WriteLine($"Student selected: {student.FullName}, Path: {path}");

            // Populate tree view with selected student's directory structure
            PopulateTreeView(path);
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

        // --- Student Selection ------------------------------------------------

        // NOTE: This method is no longer used - tree view handles navigation now
        /*
        private async void ListBoxStudents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CloseSidePanel();
            if (listBoxStudents.SelectedItem is not Student student) return;

            var path = student.Folder;
            if (path == null) return;

            var hasZipFiles = Directory.Exists(path) &&
                              Directory.GetFiles(path, "*.zip", SearchOption.AllDirectories).Length > 0;

            if (hasZipFiles)
            {
                var progressDialog = GetOrCreateProgressDialog();

                var submissionFolders = Directory.GetDirectories(path)
                    .Where(d => Path.GetFileName(d)
                        .StartsWith("submission_", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(d => d)
                    .ToList();

                if (submissionFolders.Any())
                {
                    foreach (var submissionFolder in submissionFolders)
                        await ZipFileHandler.ExtractZipFilesInFolderWithProgressAsync(
                            submissionFolder, progressDialog);
                }
                else
                {
                    await ZipFileHandler.ExtractZipFilesInFolderWithProgressAsync(
                        path, progressDialog);
                }

                if (_progressDialog?.IsVisible == true)
                    _progressDialog.Close();
            }

            // Always consolidate if submission folders exist regardless of whether zips were extracted
            var hasSubmissionFolders = Directory.Exists(path) &&
                                       Directory.GetDirectories(path)
                                           .Any(d => Path.GetFileName(d)
                                               .StartsWith("submission_", StringComparison.OrdinalIgnoreCase));

            if (hasSubmissionFolders)
            {
                Debug.WriteLine("=== Running ConsolidateSubmissions ===");
                Assignment.ConsolidateSubmissions(path);
            }

            var assignments = Assignment.FindLabOrPracticalSubfolders(path)
                .Where(a => !a.Folder!
                    .Split(Path.DirectorySeparatorChar)
                    .Any(segment => segment == ".vs"));

            listBoxAssignments.Items.Clear();
            Debug.WriteLine($"=== Found {assignments.Count()} assignments ===");

            // If no assignments found, add the root folder itself
            if (!assignments.Any())
            {
                Debug.WriteLine("No Lab/Practical folders found, adding root folder");
                var studentName = Path.GetFileName(path);
                assignments = new List<Assignment> { new Assignment($"All Files ({studentName})", path) };
            }

            foreach (var assignment in assignments)
            {
                Debug.WriteLine($"  Adding assignment: {assignment.Name}");
                listBoxAssignments.Items.Add(assignment);
            }
        }
        */

        private ExtractionProgressDialog GetOrCreateProgressDialog()
        {
            if (_progressDialog == null || !_progressDialog.IsVisible)
            {
                _progressDialog = new ExtractionProgressDialog
                {
                    Owner = this
                };

                _progressDialog.Closed += (s, e) =>
                {
                    this.Activate();
                    this.Focus();
                };

                _progressDialog.Show();
            }
            return _progressDialog;
        }

        // --- Assignment Selection ---------------------------------------------

        // NOTE: This method is no longer used - tree view handles navigation now
        /*
        private void ListBoxAssignments_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("=== ListBoxAssignments_SelectionChanged called ===");

            // Ignore if this is just a clear operation
            if (e.AddedItems.Count == 0)
            {
                Debug.WriteLine("No items added, ignoring");
                return;
            }

            ClearFileTabs();

            if (listBoxAssignments.SelectedItem is not Assignment selectedAssignment)
            {
                Debug.WriteLine("No assignment selected");
                return;
            }

            var name = selectedAssignment.Name;
            var path = selectedAssignment.Folder;
            Debug.WriteLine($"Assignment: {name}, Path: {path}");

            if (name == null || path == null)
            {
                Debug.WriteLine("Name or path is null");
                return;
            }

            // Show all subdirectories with files
            PopulateFileTabsWithDirectories(path);

            // Load JSON results and populate grade sheet form
            var labResults = FileHandler.LoadLabResults(path);
            var selectedStudent = listBoxStudents.SelectedItem as Student;
            _gradingView.LoadResults(labResults ?? new LabResults(), selectedStudent);

            UpdateViolationsStatus();
        }
        */

        // --- File Tabs --------------------------------------------------------

        private void ClearFileTabs()
        {
            foreach (var tab in panelFileTabs.Children.OfType<WpfButton>())
            {
                if (tab.Tag is string tabPath)
                {
                    PersistCommentsForFile(tabPath);
                }
            }

            panelFileTabs.Children.Clear();
            _selectedTabButton = null;
            codeEditor.Text = string.Empty;
            _violationHighlighter.Clear();
            _gradingView.Clear();
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
                panelFileTabs.Children.Add(btn);

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
                foreach (WpfButton tab in panelFileTabs.Children)
                {
                    if (tab.Tag is string existingPath && existingPath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectTab(tab);
                        Debug.WriteLine($"File already open, switching to tab: {filePath}");
                        return;
                    }
                }

                var isFirstTab = panelFileTabs.Children.Count == 0;
                var tabButton = CreateFileTab(filePath, isFirstTab);
                panelFileTabs.Children.Add(tabButton);
                SelectTab(tabButton);

                if (panelFileTabs.Children.Count == 1)
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
                string pathToOpen = selectedItem.IsDirectory 
                    ? selectedItem.FullPath 
                    : Path.GetDirectoryName(selectedItem.FullPath) ?? selectedItem.FullPath;

                Process.Start("explorer.exe", pathToOpen);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening in explorer: {ex.Message}");
                MessageBox.Show($"Error opening in File Explorer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(result, "Build", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Build failed: {ex.Message}", "Build", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RunSolutionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var solutionPath = GetSelectedSolutionPath();
            if (solutionPath == null)
                return;

            try
            {
                var service = CreateExecutionService();
                var result = await service.LaunchAsync(solutionPath);
                MessageBox.Show(result, "Run", MessageBoxButton.OK, MessageBoxImage.Information);
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
                settings.ExecutionMode = SubmissionExecutionMode.Local;
            var searchRoot = _gradingView.CurrentStudent?.Folder ?? _openedDirectoryPath;
            var identifiers = GetStudentIdentifiers(solutionPath).ToArray();
            var checkedPaths = GetCheckedFiles(fileTreeView.Items)
                .Where(file => !file.IsSolution).Select(file => file.FullPath).ToArray();
            var assignment = _currentAssignment == null ? null : new GradingAssignment
            {
                Course = _currentAssignment.Course,
                Title = _currentAssignment.Title,
                Requirements = _currentAssignment.Requirements,
                Rubric = _currentAssignment.Rubric.Select(r => new RubricItem(r.Name, r.MaxPoints)).ToList()
            };
            var draftTarget = CurrentFeedbackKey();
            var requirements = assignment?.Requirements ?? settings.RequirementsTemplate;
            // Capture metadata only; load source and create the VM when the job starts.
            if (!_aiTestQueue.TryEnqueue(solutionPath, async () =>
            {
                OpenRuntimeTerminalPanel();
                using var progress = _runtimeTerminal.BeginSession();
                AppendToRuntimeTerminal($"Testing: {solutionPath}\n", Brushes.DeepSkyBlue);
                var related = RelatedFileResolver.FindRelatedFiles(solutionPath, checkedPaths, searchRoot);
                var report = await CreateExecutionService().ExecuteAndFormatAsync(
                    solutionPath, searchRoot, requirements, identifiers, related, settings, progress);
                if (report == SubmissionExecutionPolicy.LocalDeclined) return;
                var reportDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LabFeedbackWPF", "TestReports");
                Directory.CreateDirectory(reportDirectory);
                var reportPath = Path.Combine(reportDirectory, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt");
                await File.WriteAllTextAsync(reportPath, $"Submission: {solutionPath}\n\n{report}");
                if (assignment != null && assignment.Rubric.Count > 0)
                    foreach (var path in checkedPaths)
                        await GradeFileWithRuntimeReportAsync(path, report, assignment, identifiers, checkedPaths, searchRoot, settings, draftTarget);
                AppendToRuntimeTerminal($"\nCompleted: {solutionPath}\nReport saved: {reportPath}\n", Brushes.DeepSkyBlue);
            }, out var completion))
            {
                MessageBox.Show("This solution is already running or queued, or the queue has reached its 50-test limit.",
                    "AI test queue", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Title = $"Lab Feedback - {_aiTestQueue.Count} AI test(s) running or queued";
            try { await completion; }
            catch (Exception ex)
            {
                AppendToRuntimeTerminal($"Test failed ({solutionPath}): {ex.Message}\n", Brushes.Tomato);
                Debug.WriteLine($"AI test failed ({solutionPath}): {ex}");
            }
            finally { Title = $"Lab Feedback - {_aiTestQueue.Count} AI test(s) running or queued"; }
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
            GradingAssignment assignment, string[] identifiers, string[] checkedPaths, string? searchRoot, LLMSettings settings, string draftTarget)
        {
            if (assignment == null || assignment.Rubric.Count == 0)
                return;

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
                    runtimeReport);

                feedback.StartLine = section.StartLine;
                feedback.EndLine = section.EndLine;
                TrackCommentForFile(filePath, feedback, draftTarget);
                if (draftTarget == CurrentFeedbackKey() && IsSelectedFile(filePath))
                    _commentLayer?.AddComment(feedback, section.StartLine, section.EndLine);
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
                panelFileTabs.Children.Add(btn);
                Debug.WriteLine($"Added tab for: {Path.GetFileName(filePath)}");

                if (first)
                {
                    SelectTab(btn);
                    first = false;
                }
            }

            UpdateViolationsStatus();
        }

        private WpfButton CreateFileTab(string filePath, bool first = false)
        {
            var fileName = Path.GetFileName(filePath);
            var ext = Path.GetExtension(filePath).ToLower();
            var icon = GetFileIcon(ext);

            var tabStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (icon != SymbolRegular.Document24)
            {
                tabStack.Children.Add(new SymbolIcon
                {
                    Symbol = icon,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 4, 0)
                });
            }

            tabStack.Children.Add(new WpfTextBlock
            {
                Text = fileName,
                VerticalAlignment = VerticalAlignment.Center
            });

            var closeButton = new System.Windows.Controls.Button
            {
                Content = "x",
                Width = 18,
                Height = 18,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 200, 200)),
                ToolTip = "Close file"
            };
            closeButton.Click += CloseFileTab_Click;
            closeButton.Tag = filePath;

            var dockPanel = new DockPanel();
            dockPanel.LastChildFill = true;
            dockPanel.Children.Add(tabStack);
            DockPanel.SetDock(closeButton, Dock.Right);
            dockPanel.Children.Add(closeButton);

            var btn = new WpfButton
            {
                Content = dockPanel,
                Tag = filePath,
                Margin = new Thickness(2, 0, 2, 0),
                Background = first
                    ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(255, 39, 40, 34))
            };

            btn.Click += FileTab_Click;
            return btn;
        }

        private void CloseFileTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button closeButton || closeButton.Tag is not string filePath)
                return;

            e.Handled = true;
            CloseFileTab(filePath);
        }

        private void CloseFileTab(string filePath)
        {
            PersistCommentsForFile(filePath);

            var matchingTab = panelFileTabs.Children.OfType<WpfButton>()
                .FirstOrDefault(tab => tab.Tag is string existingPath && existingPath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

            if (matchingTab != null)
            {
                panelFileTabs.Children.Remove(matchingTab);
            }

            if (_selectedTabButton != null && _selectedTabButton.Tag is string selectedPath &&
                string.Equals(selectedPath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                _selectedTabButton = null;
                if (panelFileTabs.Children.Count > 0)
                {
                    var nextTab = (WpfButton)panelFileTabs.Children[0];
                    SelectTab(nextTab);
                }
                else
                {
                    codeEditor.Text = string.Empty;
                    _violationHighlighter.Clear();
                    SetEmptyState(true);
                }
            }

            if (panelFileTabs.Children.Count == 0)
            {
                _selectedTabButton = null;
                codeEditor.Text = string.Empty;
                _violationHighlighter.Clear();
                SetEmptyState(true);
            }
        }

        private SymbolRegular GetFileIcon(string extension)
        {
            return extension switch
            {
                ".cpp" or ".cc" or ".cxx" or ".c" or ".h" or ".hpp" or ".hxx" => SymbolRegular.Code24,
                ".cs" or ".java" or ".py" or ".js" or ".ts" => SymbolRegular.Code24,
                ".txt" => SymbolRegular.Document24,
                ".json" or ".xml" => SymbolRegular.Document24,
                _ => SymbolRegular.Document24
            };
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
            Debug.WriteLine($"Scanning {panelFileTabs.Children.Count} file tabs...");

            foreach (WpfButton tab in panelFileTabs.Children)
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

            statusViolations.Text = allViolations.Count.ToString();
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

                statusViolations.ToolTip = new ToolTip
                {
                    Content = new WpfTextBlock
                    {
                        Text = sb.ToString().TrimEnd(),
                        FontFamily = new FontFamily("Consolas"),
                        MaxWidth = 600,
                        TextWrapping = TextWrapping.Wrap
                    },
                    MaxWidth = 620
                };
            }
            else
            {
                statusViolations.ToolTip = null;
            }

            statusViolations.Foreground = allViolations.Count switch
            {
                > 3 => Brushes.Red,
                > 0 => Brushes.Orange,
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"))
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
                        statusBuilds.Text = results[0].Number.ToString();
                        Debug.WriteLine($"  Builds: {results[0].Number}");

                        if (results.Count > 1)
                        {
                            statusScore.Text = results[1].Number.ToString();
                            Debug.WriteLine($"  Score: {results[1].Number}");
                        }
                        else
                        {
                            statusScore.Text = "N/A";
                            Debug.WriteLine($"  Score: N/A (not found in log)");
                        }
                    }
                    else
                    {
                        // For hdkvkt.txt or other formats: results.Count is build count, last result is score
                        statusBuilds.Text = results.Count.ToString();
                        Debug.WriteLine($"  Builds: {results.Count}");

                        statusScore.Text = results[^1].Number.ToString();
                        Debug.WriteLine($"  Score: {results[^1].Number}");
                    }
                }
                else
                {
                    Debug.WriteLine("  No results parsed from file");
                    statusBuilds.Text = "0";
                    statusScore.Text = "N/A";
                }
            }
            else
            {
                Debug.WriteLine($"  Log file not found or empty path");
                Debug.WriteLine($"  Searched for: *.fslog, *.log in {Path.GetDirectoryName(resultsPath) ?? "(unknown)"}");

                statusBuilds.Text = "0";
                statusScore.Text = "N/A";
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
            sidePanelColumn.Width = new GridLength(_sidePanelWidth);
        }

        private void CloseSidePanel()
        {
            // Save current width before closing
            _sidePanelWidth = sidePanelColumn.Width.Value > 0
                ? sidePanelColumn.Width.Value
                : 260;

            sidePanelSplitter.Visibility = Visibility.Collapsed;
            sidePanelColumn.Width = new GridLength(0);
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
