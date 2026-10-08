using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
namespace GuidedGrade.Views;
// The pinned framework has no tab or link primitive. Use its native adapter.
internal sealed class NavigationTabView(NavigationTabViewModel model)
{
 internal View Build() => WpfUI.Native(() =>
 {
  var tab = new Button { Content = model.Label, Style = NativeTheme.NavigationTabStyle(),
      Padding = new Thickness(10, 6, 10, 6), FontSize = 13, MinHeight = 32 };
  // Local template survives the host's implicit action-button theme.
  tab.Template = (ControlTemplate)tab.Style.Setters.OfType<Setter>().Single(s => s.Property == Control.TemplateProperty).Value;
  tab.SetValue(NativeTheme.TabSelectedProperty, model.Selected);
  tab.ToolTip = model.AccessibilityLabel;
  AutomationProperties.SetName(tab, model.AccessibilityLabel);
  tab.Click += (_, _) => model.Activate();
  return tab;
 }).Id(model.AccessibilityLabel + "-" + model.Selected);
}
