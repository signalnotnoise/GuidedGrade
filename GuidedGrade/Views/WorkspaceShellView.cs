using System.Windows;
using System.Windows.Controls;
using GuidedGrade.ViewModels;
using UI_Framework;
using UI_Framework.Wpf;
using static UI_Framework.UI;

namespace GuidedGrade.Views;

// Stable native layout/splitters host the pinned framework's declarative panel views.
internal sealed class WorkspaceShellView
{
    private readonly FrameworkElement _layout;
    internal WorkspaceShellView(WorkspaceShellViewModel model) => _layout = BuildLayout(model);
    internal View Build() => WpfUI.Native(() => _layout).Id("resize-layout");
    // Preserve the existing native layout during package adoption. This adapter supplies constrained
    // vertical fill and native splitter mechanics; all surrounding content is declarative.
    private static DockPanel FillBelow(FrameworkElement header, FrameworkElement body)
    {
        var dock = new DockPanel(); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top);
        dock.Children.Add(header); dock.Children.Add(body); return dock;
    }

    private static FrameworkElement BuildLayout(WorkspaceShellViewModel model)
    {
        var navigation = new Grid();
        navigation.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star), MaxHeight = 300 });
        navigation.RowDefinitions.Add(new() { Height = new(5) });
        navigation.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star), MinHeight = 150 });
        var students = FillBelow(model.Host(model.StudentGrades), model.Host(() => new StudentsPanelView(new StudentsPanelViewModel(model.Students)).Build()));
        var files = FillBelow(model.Host(() => Text("SUBMITTED FILES").FontSize(12).Padding(10)), model.Host(() => new SubmissionFilesPanelView(new SubmissionFilesPanelViewModel(model.Files)).Build()));
        var split = new GridSplitter { Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch, ResizeDirection = GridResizeDirection.Rows };
        navigation.Children.Add(students); Grid.SetRow(split, 1); navigation.Children.Add(split); Grid.SetRow(files, 2); navigation.Children.Add(files);

        var editorLayer = new Grid();
        editorLayer.Children.Add(model.Editor); editorLayer.Children.Add(model.Comments);
        model.EmptyState.Child = model.Host(new WorkspaceEmptyStateView(new WorkspaceEmptyStateViewModel()).Build);
        editorLayer.Children.Add(model.EmptyState);
        var tabScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = model.Host(model.FileTabs) };
        var editorPane = FillBelow(tabScroll, model.Host(() => WpfUI.Native(() => editorLayer).Id("annotated-editor")));

        var panes = new Grid();
        panes.ColumnDefinitions.Add(new() { Width = new(260), MinWidth = 160 });
        panes.ColumnDefinitions.Add(new() { Width = new(5) });
        panes.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star), MinWidth = 300 });
        panes.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        panes.ColumnDefinitions.Add(model.SidePanelColumn);
        Add(panes, navigation, 0); Add(panes, new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch }, 1);
        Add(panes, editorPane, 2); Add(panes, model.SidePanelSplitter, 3);
        Add(panes, FillBelow(model.Host(() => model.PanelHeader()), model.Host(() => WpfUI.Native(() => model.SidePanels).Id("workspace-panels"))), 4);


        model.ViolationsPanel.Child = model.Host(() => new ViolationsPanelView(new ViolationsPanelViewModel(model.Violations)).Build());
        model.ConsolePanel.Child = model.Host(() => new ConsolePanelView(new ConsolePanelViewModel(model.Console)).Build());
        model.LogPanel.Child = model.Host(() => new LogPanelView(model.Log).Build());
        model.ToolsContent.Children.Add(model.LogPanel);
        model.ToolsContent.Children.Add(model.ViolationsPanel); model.ToolsContent.Children.Add(model.ConsolePanel);
        var workspace = new Grid();
        workspace.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        workspace.RowDefinitions.Add(new() { Height = GridLength.Auto });
        workspace.RowDefinitions.Add(model.ToolsRow);
        workspace.Children.Add(panes);
        Grid.SetRow(model.ToolsContent, 2); workspace.Children.Add(model.ToolsContent);
        var shell = new DockPanel();
        var header = model.Host(model.Header); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); shell.Children.Add(header);
        var status = model.Host(model.ToolTabs); DockPanel.SetDock(status, System.Windows.Controls.Dock.Bottom); shell.Children.Add(status);
        shell.Children.Add(workspace);
        return shell;
    }
    private static void Add(Grid grid, FrameworkElement child, int column)
    { Grid.SetColumn(child, column); grid.Children.Add(child); }
}
