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

    // Preserve the existing native layout during package adoption. This adapter supplies constrained
    // vertical fill and native splitter mechanics; all surrounding content is declarative.
    private static DockPanel FillBelow(FrameworkElement header, FrameworkElement body)
    {
        var dock = new DockPanel(); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top);
        dock.Children.Add(header); dock.Children.Add(body); return dock;
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

        var navigation = new Grid();
        navigation.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star), MaxHeight = 300 });
        navigation.RowDefinitions.Add(new() { Height = new(5) });
        navigation.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star), MinHeight = 150 });
        var students = FillBelow(ShellHost(BuildGradeScope), ShellHost(() => new StudentsPanelView(new StudentsPanelViewModel(listBoxStudents)).Build()));
        var files = FillBelow(ShellHost(() => Text("SUBMITTED FILES").FontSize(12).Padding(10)), ShellHost(() => new SubmissionFilesPanelView(new SubmissionFilesPanelViewModel(fileTreeView)).Build()));
        var split = new GridSplitter { Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch, ResizeDirection = GridResizeDirection.Rows };
        navigation.Children.Add(students); Grid.SetRow(split, 1); navigation.Children.Add(split); Grid.SetRow(files, 2); navigation.Children.Add(files);

        var editorLayer = new Grid();
        editorLayer.Children.Add(codeEditor); editorLayer.Children.Add(commentOverlay);
        emptyStateOverlay.Child = ShellHost(new WorkspaceEmptyStateView(new WorkspaceEmptyStateViewModel()).Build);
        editorLayer.Children.Add(emptyStateOverlay);
        var tabScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = ShellHost(BuildFileTabs) };
        var editorPane = FillBelow(tabScroll, ShellHost(() => WpfUI.Native(() => editorLayer).Id("annotated-editor")));

        var panes = new Grid();
        panes.ColumnDefinitions.Add(new() { Width = new(260), MinWidth = 160 });
        panes.ColumnDefinitions.Add(new() { Width = new(5) });
        panes.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star), MinWidth = 300 });
        panes.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        panes.ColumnDefinitions.Add(sidePanelColumn);
        Add(panes, navigation, 0); Add(panes, new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch }, 1);
        Add(panes, editorPane, 2); Add(panes, sidePanelSplitter, 3);
        Add(panes, FillBelow(ShellHost(() => new PanelHeaderView(new PanelHeaderViewModel(_activePanel, CloseSidePanel)).Build()), ShellHost(() => WpfUI.Native(() => rightPanelTabs).Id("workspace-panels"))), 4);


        violationsPanel.Child = ShellHost(() => new ViolationsPanelView(new ViolationsPanelViewModel(violationsList)).Build());
        runtimeTerminalPanel.Child = ShellHost(() => new ConsolePanelView(new ConsolePanelViewModel(runtimeTerminalRichTextBox)).Build());
        _logPanel.Child = ShellHost(() => new LogPanelView(_logModel).Build());
        toolsPanelContent.Children.Add(_logPanel);
        toolsPanelContent.Children.Add(violationsPanel); toolsPanelContent.Children.Add(runtimeTerminalPanel);
        var workspace = new Grid();
        workspace.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        workspace.RowDefinitions.Add(new() { Height = GridLength.Auto });
        workspace.RowDefinitions.Add(toolsPanelRow);
        workspace.Children.Add(panes);
        Grid.SetRow(toolsPanelContent, 2); workspace.Children.Add(toolsPanelContent);
        var shell = new DockPanel();
        var header = ShellHost(BuildHeader); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); shell.Children.Add(header);
        var status = ShellHost(BuildToolTabs); DockPanel.SetDock(status, System.Windows.Controls.Dock.Bottom); shell.Children.Add(status);
        shell.Children.Add(workspace);
        Content = _workspaceHost = ReviewTheme.Host(() => WpfUI.Native(() => shell).Id("resize-layout"));
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

    private static void Add(Grid grid, FrameworkElement child, int column)
    { Grid.SetColumn(child, column); grid.Children.Add(child); }
}
