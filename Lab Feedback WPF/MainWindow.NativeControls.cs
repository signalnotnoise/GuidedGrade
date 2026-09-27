using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Lab_Feedback_WPF.Presentation;

namespace Lab_Feedback_WPF;

public partial class MainWindow
{
    // Stable specialist controls: framework redraws must not replace documents or selection.
    internal readonly ICSharpCode.AvalonEdit.TextEditor codeEditor = new()
    {
        FontFamily = new("Consolas"), FontSize = 13, IsReadOnly = true,
        ShowLineNumbers = true, Background = Brush("#1B1E23"), Foreground = Brush("#F8F8F2")
    };
    internal readonly TreeView fileTreeView = new() { BorderThickness = new(0) };
    internal readonly ListBox listBoxStudents = new() { BorderThickness = new(0), DisplayMemberPath = "FullName" };
    internal readonly RichTextBox runtimeTerminalRichTextBox = new()
    {
        Background = Brush("#0C0C0C"), Foreground = Brush("#CCCCCC"), FontFamily = new("Consolas"),
        FontSize = 13, BorderThickness = new(0), IsReadOnly = true, IsReadOnlyCaretVisible = false,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        Padding = new(8)
    };
    private readonly Canvas commentOverlay = new() { ClipToBounds = true };
    private readonly Border emptyStateOverlay = new() { Background = Brush("#1B1E23") };
    private readonly ListView violationsList = new() { BorderThickness = new(0), FontFamily = new("Consolas"), FontSize = 12 };
    internal readonly TabControl rightPanelTabs = new() { BorderThickness = new(0) };
    internal readonly TabItem commentsDetailsTab = new() { Header = "Comments" };
    internal readonly TabItem rubricDetailsTab = new() { Header = "Rubric" };
    internal readonly TabItem queueDetailsTab = new() { Header = "Job queue" };
    private readonly ColumnDefinition sidePanelColumn = new() { Width = new(0) };
    private readonly GridSplitter sidePanelSplitter = new() { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, Visibility = Visibility.Collapsed };
    private readonly RowDefinition toolsPanelRow = new() { Height = new(0) };
    private readonly Grid toolsPanelContent = new() { Visibility = Visibility.Collapsed };
    private readonly Border violationsPanel = new() { Visibility = Visibility.Collapsed };
    private readonly Border runtimeTerminalPanel = new() { Visibility = Visibility.Collapsed };

    private void InitializeNativeControls()
    {
        Title = "Assignment Review"; Width = 1200; Height = 800; MinWidth = 1000; MinHeight = 600;
        listBoxStudents.SelectionChanged += ListBoxStudents_SelectionChanged;
        fileTreeView.SelectedItemChanged += FileTreeView_SelectedItemChanged;
        fileTreeView.PreviewMouseRightButtonDown += FileTreeView_PreviewMouseRightButtonDown;
        violationsList.MouseDoubleClick += ViolationsList_MouseDoubleClick;
        foreach (var control in new Control[] { fileTreeView, listBoxStudents, violationsList, rightPanelTabs })
        {
            control.Background = Brush("#1B1E23"); control.Foreground = Brush("#EEEEEE");
        }
        AutomationProperties.SetName(codeEditor, "Submission source");
        AutomationProperties.SetName(fileTreeView, "Submitted files");
        AutomationProperties.SetName(listBoxStudents, "Students");
        AutomationProperties.SetName(runtimeTerminalRichTextBox, "Runtime console");
        ConfigureFileTree();
        ConfigureViolations();
        codeEditor.ContextMenu = new ContextMenu();
        codeEditor.ContextMenu.Items.Add(MenuAction("Grade Selected Section...", GradeSection_Click));
        ReviewTheme.ApplyNativeStyles(codeEditor.ContextMenu);
        var hiddenHeader = new Style(typeof(TabItem));
        hiddenHeader.Setters.Add(new Setter(Control.TemplateProperty,
            new ControlTemplate(typeof(TabItem)) { VisualTree = new FrameworkElementFactory(typeof(Border)) }));
        rightPanelTabs.ItemContainerStyle = hiddenHeader;
        rightPanelTabs.Items.Add(commentsDetailsTab);
        rightPanelTabs.Items.Add(rubricDetailsTab);
        rightPanelTabs.Items.Add(queueDetailsTab);
        rightPanelTabs.SelectionChanged += (_, _) => UpdatePinnedTabs();
    }

    private void ConfigureFileTree()
    {
        VirtualizingPanel.SetIsVirtualizing(fileTreeView, true);
        VirtualizingPanel.SetVirtualizationMode(fileTreeView, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(fileTreeView, true);
        fileTreeView.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        var row = new FrameworkElementFactory(typeof(StackPanel));
        row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var check = new FrameworkElementFactory(typeof(CheckBox));
        check.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        check.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsCheckedForAnalysis") { Mode = BindingMode.TwoWay });
        check.SetBinding(UIElement.VisibilityProperty, new Binding("IsDirectory") { Converter = new Converters.InverseBoolToVisibilityConverter() });
        check.SetBinding(AutomationProperties.NameProperty, new Binding("Name") { StringFormat = "Include {0} in review" });
        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetBinding(TextBlock.TextProperty, new Binding("Name"));
        row.AppendChild(check); row.AppendChild(label);
        fileTreeView.ItemTemplate = new HierarchicalDataTemplate { ItemsSource = new Binding("Children"), VisualTree = row };
        var itemStyle = new Style(typeof(TreeViewItem));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#EEEEEE")));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 3, 4, 3)));
        itemStyle.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, new Binding("IsExpanded") { Mode = BindingMode.TwoWay }));
        itemStyle.Setters.Add(new Setter(TreeViewItem.IsSelectedProperty, new Binding("IsSelected") { Mode = BindingMode.TwoWay }));
        itemStyle.Setters.Add(new Setter(ItemsControl.ItemsPanelProperty,
            new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)))));
        var solution = new DataTrigger { Binding = new Binding("IsSolution"), Value = true };
        solution.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#4EC9B0")));
        solution.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        itemStyle.Triggers.Add(solution);
        fileTreeView.ItemContainerStyle = itemStyle;
        var menu = fileTreeView.ContextMenu = new ContextMenu();
        menu.Opened += FileTreeContextMenu_Opened;
        var programming = new MenuItem { Header = "Programming tools", Tag = "solution-command" };
        programming.Items.Add(MenuAction("Build", BuildSolutionMenuItem_Click));
        programming.Items.Add(MenuAction("Build and Run", BuildAndRunSolutionMenuItem_Click));
        programming.Items.Add(MenuAction("Run", RunSolutionMenuItem_Click, "Runs existing output without rebuilding."));
        programming.Items.Add(MenuAction("Test with AI", TestSolutionWithAiMenuItem_Click, "Uses the saved Local or VM execution environment."));
        var local = MenuAction("Test with AI locally...", TestSolutionWithAiMenuItem_Click, "Runs on this computer after a warning. Use only for trusted submissions.");
        local.CommandParameter = "local"; programming.Items.Add(local);
        menu.Items.Add(programming); menu.Items.Add(new Separator());
        menu.Items.Add(MenuAction("Open in File Explorer", OpenTreeItemInExplorer_Click));
        menu.Items.Add(MenuAction("Refresh", RefreshTreeView_Click));
        var clear = MenuAction("Clear review", ClearReviewMenuItem_Click, "Deletes saved and inline comments for this file. The combined editable feedback draft is kept.");
        clear.Tag = "file-command"; menu.Items.Add(clear); menu.Items.Add(new Separator());
        menu.Items.Add(MenuAction("Overall feedback for checked files", AnalyzeWithLLM_Click, "Uses the selected assignment's requirements and rubric."));
        menu.Items.Add(MenuAction("Section-by-section comments for checked files", AnalyzeSectionsMenuItem_Click, "Requires an assignment with a rubric. Reports files without detected sections."));
        ReviewTheme.ApplyNativeStyles(menu);
    }

    private void ConfigureViolations()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        foreach (var (path, format, color) in new[] { ("LineNumber", "Line {0}: ", "#569CD6"), ("MatchedText", "{0} — ", "#CE9178"), ("Context", "{0}", "#9CDCFE") })
        {
            var run = new FrameworkElementFactory(typeof(Run));
            run.SetBinding(Run.TextProperty, new Binding(path) { StringFormat = format });
            run.SetValue(TextElement.ForegroundProperty, Brush(color)); text.AppendChild(run);
        }
        violationsList.ItemTemplate = new DataTemplate { VisualTree = text };
    }

    private static MenuItem MenuAction(string title, RoutedEventHandler action, string? tip = null)
    {
        var item = new MenuItem { Header = title, ToolTip = tip }; item.Click += action; return item;
    }
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
}
