using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GuidedGrade.Presentation;

internal static class SearchBarNativeAdapter
{
    internal static void FocusQuery(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBox field)
            {
                // Preserve the native editing content host inside the framework field.
                var border = new FrameworkElementFactory(typeof(Border));
                border.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(ReviewTheme.Tokens.Surface)));
                border.SetValue(Border.BorderBrushProperty, Brushes.SlateGray);
                border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
                border.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));
                var scroll = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
                scroll.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                border.AppendChild(scroll);
                field.Template = new ControlTemplate(typeof(TextBox)) { VisualTree = border };
                field.FontSize = 13;
                field.Focus(); field.SelectAll(); return;
            }
            FocusQuery(child);
        }
    }

}