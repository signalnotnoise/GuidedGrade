using GuidedGrade.Presentation;
using GuidedGrade.Views;
using GuidedGrade.ViewModels;
using UI_Framework;
using UI_Framework.Wpf;
using static UI_Framework.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GuidedGrade;

public partial class MainWindow
{
    private ViewHost? _workspaceHost;
    private readonly List<ViewHost> _shellHosts = [];
    private readonly StateList<FileTab> _fileTabs = new();
    private readonly State<string> _activeFile = new("");
    private readonly State<string> _buildCount = new("0");
    private readonly State<string> _score = new("0");
    private readonly State<string> _violationCount = new("0");
    private readonly State<string> _violationColor = new("#CCCCCC");
    private readonly ToolTip _violationTooltip = new();
    private readonly State<string> _queueSummary = new("Queue: 0 left");
    private readonly State<int> _activePanel = new(-1);
    private readonly State<bool> _showComments = new(true);
    private readonly State<bool> _showQueue = new(true);
    private readonly State<bool> _toolsVisible = new(false);
    private readonly State<bool> _violationsSelected = new(false);
    private readonly StateList<string> _courses = new();
    private readonly StateList<string> _assignments = new();
    private readonly State<int> _courseIndex = new(-1);
    private readonly State<int> _assignmentIndex = new(-1);


    private ViewHost ShellHost(Func<View> body)
    {
        var host = ReviewTheme.Host(body); _shellHosts.Add(host); return host;
    }

    private Menu? _applicationMenu;

    // The pinned framework has no menu primitive. Keep native menu keyboard and
    // popup behavior inside its adapter, with stable identity across picker updates.
    private Menu ApplicationMenu()
    {
        if (_applicationMenu != null) return _applicationMenu;
        MenuItem Action(string label, RoutedEventHandler handler)
        {
            var item = new MenuItem { Header = label };
            item.Click += handler;
            return item;
        }
        var file = new MenuItem { Header = "_File" };
        file.Items.Add(Action("_Open Submissions Folder", OpenFolderMenuItem_Click));
        file.Items.Add(new Separator());
        file.Items.Add(Action("E_xit", ExitMenuItem_Click));
        var settings = new MenuItem { Header = "_Settings" };
        settings.Items.Add(Action("_Programming Checks...", SettingsMenuItem_Click));
        settings.Items.Add(Action("_AI Provider...", LLMSettingsMenuItem_Click));
        settings.Items.Add(new Separator());
        settings.Items.Add(Action("_Setup Assignment...", SetupAssignmentMenuItem_Click));
        _applicationMenu = new Menu();
        _applicationMenu.Items.Add(file);
        _applicationMenu.Items.Add(settings);
        return _applicationMenu;
    }

    private View BuildHeader() => new WorkspaceToolbarView(new WorkspaceToolbarViewModel(_courses, _assignments, _courseIndex, _assignmentIndex, ApplicationMenu, SelectCourse, SelectAssignment, () => WorkspaceAssignment_Click(this, new()), () => OpenFolderMenuItem_Click(this, new()), () => WorkspaceRubric_Click(this, new()), () => WorkspaceFeedback_Click(this, new()), DesignBatchReview, BuildNavigation, BuildReviewMenu)).Build();

    private View BuildFileTabs() => new FileTabsView(new FileTabsViewModel(_fileTabs, _activeFile, SelectTab, CloseFileTab)).Build();

    private View BuildNavigation() => new PanelNavigationView(new PanelNavigationViewModel(_showComments, _showQueue, _activePanel, () => WorkspaceFeedback_Click(this, new()), () => ShowQueue_Click(this, new()), () => WorkspaceRubric_Click(this, new()), CloseSidePanel)).Build();

    private View BuildToolTabs() => new ToolsPanelToolbarView(new ToolsPanelToolbarViewModel(_toolsVisible, _violationsSelected, _logSelected, SelectLogPanel, LogOptions, _violationCount, BuildStatus, () => SelectToolsPanel(false, true), () => SelectToolsPanel(true, true), () => SetToolsPanelVisible(!_toolsVisible.Value))).Build();

    private View BuildStatus() => new WorkspaceStatusView(new WorkspaceStatusViewModel(_showQueue, _queueSummary, _logModel.RecordedBuildCount, _logModel.IsLoaded, _violationCount, _violationColor, () => ShowQueue_Click(this, new()), () => SelectToolsPanel(true, true), BuildGradeStatus)).Build();

    private void InitializeFrameworkShell()
    {
        ReviewTheme.Apply(this);
        InitializeNativeControls();
        commentsDetailsTab.Content = ShellHost(() => Text("Select a student to prepare comments and feedback.").Padding(20));
        commentsDetailsTab.SizeChanged += (_, _) => _feedbackHeight.Value = Math.Max(160, commentsDetailsTab.ActualHeight * 0.5);

        var model = new WorkspaceShellViewModel
        {
            Host = ShellHost, StudentGrades = BuildGradeScope, FileTabs = BuildFileTabs,
            Header = BuildHeader, ToolTabs = BuildToolTabs,
            PanelHeader = () => new PanelHeaderView(new PanelHeaderViewModel(_activePanel, CloseSidePanel)).Build(),
            Students = listBoxStudents, Files = fileTreeView, Editor = codeEditor, Comments = commentOverlay,
            EmptyState = emptyStateOverlay, SidePanelColumn = sidePanelColumn, SidePanelSplitter = sidePanelSplitter,
            SidePanels = rightPanelTabs, ViolationsPanel = violationsPanel, Violations = violationsList,
            ConsolePanel = runtimeTerminalPanel, Console = runtimeTerminalRichTextBox, LogPanel = _logPanel, Log = _logModel,
            ToolsContent = toolsPanelContent, ToolsRow = toolsPanelRow
        };
        var shell = new WorkspaceShellView(model);
        Content = _workspaceHost = ReviewTheme.Host(shell.Build);
        Closed += (_, _) =>
        {
            _commentLayer?.ClearComments();
            _feedbackHost?.Dispose(); _feedbackHost = null;
            _rubricHost?.Dispose(); _rubricHost = null;
            _gradingView.Dispose();
            foreach (var host in _shellHosts) host.Dispose();
            _shellHosts.Clear();
            _workspaceHost?.Dispose(); _workspaceHost = null;
        };
    }

}
