using GuidedGrade.ViewModels;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using GuidedGrade.Models;
using GuidedGrade.Services;
using GuidedGrade.Views;
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
        private double _sidePanelWidth = 380;
        private string? _openedDirectoryPath;

        // Section grading
        private Models.GradingAssignment? _currentAssignment;
        private Controls.InlineCommentLayer? _commentLayer;
        private readonly Services.CommentPersistenceService _commentPersistenceService;
        private readonly ReviewWorkspaceService _reviewWorkspace;
        private BoundedCache<List<Models.SectionFeedback>> _fileComments => _reviewWorkspace.Comments;
        private string? _draftSaveError;


        public MainWindow() : this(new AssignmentPersistenceService(), new CommentPersistenceService()) { }

        internal MainWindow(AssignmentPersistenceService assignments, CommentPersistenceService comments)
        {
            _assignmentPersistenceService = assignments;
            _reviewDrafts = new ReviewDraftStore(assignments.DatabasePath);
            _reviewWorkspace = new ReviewWorkspaceService(comments, () => _selectedTabButton?.Tag as string);
            Closing += (_, e) =>
            {
                if (_draftSaveError != null && MessageBox.Show(this, "Your latest feedback edits could not be saved: " + _draftSaveError + "\nClose anyway? Export feedback to keep a copy.", "Unsaved feedback", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) e.Cancel = true;
            };
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
            _commentLayer.ModifyRequested += CommentLayer_ModifyRequested;
            _commentLayer.PlacementChanged += CommentLayer_PlacementChanged;
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

        private readonly Services.ReviewGeneration _reviewGeneration = new();

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

        private void OpenSidePanel()
        {
            sidePanelSplitter.Visibility = Visibility.Visible;
            sidePanelColumn.Width = new GridLength(Math.Min(_sidePanelWidth, Math.Max(300, ActualWidth * 0.35)));
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

    }
}
