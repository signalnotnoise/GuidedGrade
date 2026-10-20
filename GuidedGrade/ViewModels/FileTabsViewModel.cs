using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed record FileTab(string Tag);
internal sealed class FileTabsViewModel(StateList<FileTab> tabs, State<string> activeFile, Action<FileTab> select, Action<string> close)
{
 internal StateList<FileTab> Tabs { get; } = tabs;
 internal State<string> ActiveFile { get; } = activeFile;
 internal Action<FileTab> Select { get; } = select;
 internal Action<string> Close { get; } = close;
}
