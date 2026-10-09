using System.Windows.Controls;
using ICSharpCode.AvalonEdit;
using UI_Framework;
using UI_Framework.Wpf;

namespace GuidedGrade.ViewModels;

// Native references are intentional adapters for editor, tree, terminal and splitters.
internal sealed class WorkspaceShellViewModel
{
    internal required Func<Func<View>, ViewHost> Host { get; init; }
    internal required Func<View> StudentGrades { get; init; }
    internal required Func<View> FileTabs { get; init; }
    internal required Func<View> Header { get; init; }
    internal required Func<View> ToolTabs { get; init; }
    internal required Func<View> PanelHeader { get; init; }
    internal required ListBox Students { get; init; }
    internal required TreeView Files { get; init; }
    internal required TextEditor Editor { get; init; }
    internal required Canvas Comments { get; init; }
    internal required Border EmptyState { get; init; }
    internal required ColumnDefinition SidePanelColumn { get; init; }
    internal required GridSplitter SidePanelSplitter { get; init; }
    internal required TabControl SidePanels { get; init; }
    internal required Border ViolationsPanel { get; init; }
    internal required ListBox Violations { get; init; }
    internal required Border ConsolePanel { get; init; }
    internal required RichTextBox Console { get; init; }
    internal required Border LogPanel { get; init; }
    internal required LogPanelViewModel Log { get; init; }
    internal required Grid ToolsContent { get; init; }
    internal required RowDefinition ToolsRow { get; init; }
}
