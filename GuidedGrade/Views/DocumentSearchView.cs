using GuidedGrade.ViewModels;
using UI_Framework;
using UI_Framework.Wpf;

namespace GuidedGrade.Views;

internal sealed class DocumentSearchView(DocumentSearchViewModel model)
{
    internal View Build() => WpfUI.Native(() => model.Editor).Id("searchable-document");
}
