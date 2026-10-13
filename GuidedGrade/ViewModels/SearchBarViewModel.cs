using UI_Framework;
using ICSharpCode.AvalonEdit.Search;

namespace GuidedGrade.ViewModels;

internal sealed class SearchBarViewModel(DocumentSearchViewModel search)
{
    internal Binding<string> Query { get; } = new(() => search.Panel.SearchPattern ?? "", value => search.Panel.SearchPattern = value);
    internal void Previous() => search.Panel.FindPrevious();
    internal void Next() => search.Panel.FindNext();
    internal void Close() => search.Close();
}
