using System.Windows.Automation;
using System.Windows.Controls;
using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.ViewModels;
namespace GuidedGrade.Views;
internal sealed class MenuSelectorView(MenuSelectorViewModel model)
{
 internal View Build() => WpfUI.Native(() =>
 {
  var menu = new Menu();
  var selector = new MenuItem { Header = model.Header };
  AutomationProperties.SetName(selector, model.AccessibilityLabel);
  for (var index = 0; index < model.Items.Length; index++)
  {
   var captured = index;
   var option = new MenuItem { Header = model.Items[index], IsCheckable = true, IsChecked = index == model.SelectedIndex };
   option.Click += (_, _) => model.Select(captured);
   selector.Items.Add(option);
  }
  if (model.Items.Length == 0) selector.Items.Add(new MenuItem { Header = "No saved " + model.Label.ToLowerInvariant() + "s", IsEnabled = false });
  menu.Items.Add(selector);
  return menu;
 }).Id(model.Identity);
}
