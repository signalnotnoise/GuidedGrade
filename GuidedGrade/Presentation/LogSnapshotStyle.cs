using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace GuidedGrade.Presentation;

internal static class LogSnapshotStyle
{
    internal static void Apply(ListBox list)
    {
        Brush Color(string value) => new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(value));
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Color(ReviewTheme.Tokens.Ink)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Color(ReviewTheme.Tokens.Canvas)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.PaddingProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { RelativeSource = RelativeSource.TemplatedParent });
        border.AppendChild(content);
        // Own the selection surface so the OS inactive-selection template cannot
        // replace the background while leaving our foreground white.
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ListBoxItem)) { VisualTree = border }));
        var match = new DataTrigger { Binding = new Binding("MatchesSearch"), Value = true };
        match.Setters.Add(new Setter(Control.ForegroundProperty,
            Color(ReviewTheme.Tokens.Canvas == "#F5F6F8" ? "#875900" : "#FFD700")));
        match.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        style.Triggers.Add(match);
        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, Color("#005A8C")));
        selected.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        style.Triggers.Add(selected);
        list.ItemContainerStyle = style;
    }
}
