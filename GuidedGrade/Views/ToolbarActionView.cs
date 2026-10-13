using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using UI_Framework;
using UI_Framework.Wpf;
namespace GuidedGrade.Views;
internal sealed class ToolbarActionView(ToolbarActionViewModel model, Func<object>? content = null)
{
    internal View Build() => WpfUI.Native(() =>
    {
        var button = new Button { Content = content?.Invoke() ?? model.Label, Style = NativeTheme.NavigationTabStyle(), Padding = content == null ? new Thickness(10, 6, 10, 6) : new Thickness(6), MinHeight = 32, FontSize = model.FontSize, IsEnabled = model.Enabled,
            Foreground = (Brush)new BrushConverter().ConvertFromString(ReviewTheme.ColorFor(model.Color))! };
        button.Template = (ControlTemplate)button.Style.Setters.OfType<Setter>().Single(s => s.Property == Control.TemplateProperty).Value;
        if (content != null) button.Template = IconTemplate();
        button.ToolTip = model.Hint ?? model.AccessibilityLabel;
        AutomationProperties.SetName(button, model.AccessibilityLabel);
        button.Click += (_, _) => model.Activate();
        return button;
    }).Id(model.AccessibilityLabel + model.Label + model.Enabled + model.Color + ReviewTheme.Tokens.Canvas);

    private static ControlTemplate IconTemplate()
    {
        var surface = new FrameworkElementFactory(typeof(Border), "surface");
        surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
        surface.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        surface.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        surface.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        surface.SetValue(Border.PaddingProperty, new Thickness(5));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        surface.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = surface };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty,
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(ReviewTheme.Tokens.Hover)), "surface"));
        template.Triggers.Add(hover);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty,
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(ReviewTheme.Tokens.Focus)), "surface"));
        template.Triggers.Add(focus);
        return template;
    }
}
