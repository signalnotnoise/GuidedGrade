using UI_Framework;
using GuidedGrade.ViewModels;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class FileTabsView(FileTabsViewModel model)
{
 internal View Build() => HStack(model.Tabs.Select(tab => HStack(
  new NavigationTabView(new(System.IO.Path.GetFileName(tab.Tag), model.ActiveFile.Value == tab.Tag,
   () => model.Select(tab), "Open " + System.IO.Path.GetFileName(tab.Tag))).Build(),
  new NavigationTabView(new("×", false, () => model.Close(tab.Tag), "Close " + System.IO.Path.GetFileName(tab.Tag))).Build()
 ).Spacing(0).Id(tab.Tag)).ToArray()).Spacing(2);
}
