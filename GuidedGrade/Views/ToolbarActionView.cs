using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using UI_Framework;
using UI_Framework.Wpf;
namespace GuidedGrade.Views;
internal sealed class ToolbarActionView(ToolbarActionViewModel model)
{
    internal View Build() => WpfUI.Native(() =>
    {
        var button = new Button { Content = model.Label, Style = NativeTheme.NavigationTabStyle(), Padding = new Thickness(10, 6, 10, 6), MinHeight = 32, FontSize = 12, IsEnabled = model.Enabled,
            Foreground = (Brush)new BrushConverter().ConvertFromString(model.Color)! };
        button.Template = (ControlTemplate)button.Style.Setters.OfType<Setter>().Single(s => s.Property == Control.TemplateProperty).Value;
        button.ToolTip = model.Hint ?? model.AccessibilityLabel;
        AutomationProperties.SetName(button, model.AccessibilityLabel);
        button.Click += (_, _) => model.Activate();
        return button;
    }).Id(model.AccessibilityLabel + model.Label + model.Enabled + model.Color);
}
