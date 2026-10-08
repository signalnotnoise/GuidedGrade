using System.Windows.Controls;
using System.Windows.Automation;
using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.ViewModels;
namespace GuidedGrade.Views;
internal sealed class ReviewMenuView(ReviewMenuViewModel model)
{
 internal View Build() => WpfUI.Native(() =>
 {
  var menu = new Menu();
  var review = new MenuItem { Header = "Review" };
  AutomationProperties.SetName(review, "Review menu");
  MenuItem Item(string label, Action action)
  {
   var item = new MenuItem { Header = label };
   item.Click += (_, _) => action();
   review.Items.Add(item); return item;
  }
  Item("Batch review", model.Batch);
  var selection = Item("Selection review", model.Selection);
  var overall = Item("Overall review", model.Overall);
  review.Items.Add(new Separator());
  var clear = Item("Clear reviews", model.Clear);
  Item("Clear all reviews", model.ClearAll);
  void Refresh() { selection.IsEnabled = model.HasSelection(); overall.IsEnabled = clear.IsEnabled = model.HasFiles(); }
  review.SubmenuOpened += (_, _) => Refresh();
  Refresh();
  menu.Items.Add(review);
  return menu;
 }).Id("review-menu");
}
