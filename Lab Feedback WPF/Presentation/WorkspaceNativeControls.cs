using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Lab_Feedback_WPF.Converters;

namespace Lab_Feedback_WPF.Presentation;

/// <summary>Specialist native list/tree presentation; the workspace owns sources, handlers and menus.</summary>
internal static class WorkspaceNativeControls
{
    internal static TreeView CreateFileTree()
    {
        var tree = new TreeView
        {
            Background = Brush("#1B1E23"), Foreground = Brush("#CCCCCC"), BorderThickness = new Thickness(0)
        };
        tree.Resources[typeof(TreeViewItem)] = FileTreeItemStyle();
        var row = Element<StackPanel>();
        row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var check = Element<CheckBox>();
        check.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsCheckedForAnalysis") { Mode = BindingMode.TwoWay });
        check.SetBinding(UIElement.VisibilityProperty, new Binding("IsDirectory") { Converter = new InverseBoolToVisibilityConverter() });
        check.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        row.AppendChild(check);
        var name = Element<TextBlock>();
        name.SetBinding(TextBlock.TextProperty, new Binding("Name"));
        name.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var labelStyle = new Style(typeof(TextBlock));
        labelStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#CCCCCC")));
        var solution = new DataTrigger { Binding = new Binding("IsSolution"), Value = true };
        solution.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#4EC9B0")));
        solution.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        labelStyle.Triggers.Add(solution);
        name.SetValue(FrameworkElement.StyleProperty, labelStyle);
        row.AppendChild(name);
        tree.ItemTemplate = new HierarchicalDataTemplate { ItemsSource = new Binding("Children"), VisualTree = row };
        return tree;
    }

    internal static ListBox CreateStudentList()
    {
        var list = new ListBox
        {
            Background = Brush("#1B1E23"), Foreground = Brush("#CCCCCC"), BorderThickness = new Thickness(0)
        };
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        var name = Element<TextBlock>();
        name.SetBinding(TextBlock.TextProperty, new Binding("FullName"));
        name.SetBinding(FrameworkElement.ToolTipProperty, new Binding("FullName"));
        name.SetValue(TextBlock.PaddingProperty, new Thickness(10, 5, 10, 5));
        name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        list.ItemTemplate = new DataTemplate { VisualTree = name };
        return list;
    }

    internal static ListView CreateViolationsList()
    {
        var list = new ListView
        {
            Background = Brushes.Transparent, Foreground = Brush("#CCCCCC"), BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Consolas"), FontSize = 12, Padding = new Thickness(8)
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        var line = Element<TextBlock>();
        var number = Element<Run>();
        number.SetBinding(Run.TextProperty, new Binding("LineNumber") { StringFormat = "Line {0}:" });
        number.SetValue(TextElement.ForegroundProperty, Brush("#569CD6"));
        line.AppendChild(number);
        line.AppendChild(TextRun(" "));
        var matched = Element<Run>();
        matched.SetBinding(Run.TextProperty, new Binding("MatchedText"));
        matched.SetValue(TextElement.ForegroundProperty, Brush("#CE9178"));
        line.AppendChild(matched);
        line.AppendChild(TextRun(" — "));
        var context = Element<Run>();
        context.SetBinding(Run.TextProperty, new Binding("Context"));
        context.SetValue(TextElement.ForegroundProperty, Brush("#9CDCFE"));
        line.AppendChild(context);
        list.ItemTemplate = new DataTemplate { VisualTree = line };
        return list;
    }

    private static Style FileTreeItemStyle()
    {
        var style = new Style(typeof(TreeViewItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#CCCCCC")));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 3, 4, 3)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
        var root = Element<StackPanel>();
        var border = Element<Border>("Border");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        var row = Element<StackPanel>();
        row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var expander = Element<ToggleButton>("Expander");
        expander.SetValue(FrameworkElement.WidthProperty, 16.0);
        expander.SetValue(FrameworkElement.HeightProperty, 16.0);
        expander.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(TreeViewItem.IsExpanded))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Mode = BindingMode.TwoWay
        });
        expander.SetValue(ButtonBase.ClickModeProperty, ClickMode.Press);
        expander.SetValue(Control.BackgroundProperty, Brushes.Transparent);
        expander.SetValue(Control.BorderThicknessProperty, new Thickness(0));
        expander.SetValue(FrameworkElement.StyleProperty, ExpanderStyle());
        row.AppendChild(expander);
        var icon = Element<TextBlock>("Icon");
        icon.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
        icon.SetValue(TextBlock.FontSizeProperty, 14.0);
        icon.SetValue(TextBlock.ForegroundProperty, Brush("#DCAA6F"));
        icon.SetValue(TextBlock.TextProperty, "\uE8B7");
        icon.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 4, 0));
        icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        row.AppendChild(icon);
        var header = Element<ContentPresenter>();
        header.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        header.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        row.AppendChild(header);
        border.AppendChild(row);
        root.AppendChild(border);
        var children = Element<ItemsPresenter>("ItemsHost");
        children.SetValue(FrameworkElement.MarginProperty, new Thickness(16, 0, 0, 0));
        root.AppendChild(children);
        var template = new ControlTemplate(typeof(TreeViewItem)) { VisualTree = root };
        template.Triggers.Add(When(ItemsControl.HasItemsProperty, false,
            Set(UIElement.VisibilityProperty, Visibility.Hidden, "Expander"),
            Set(TextBlock.TextProperty, "\uE8A5", "Icon"), Set(TextBlock.ForegroundProperty, Brush("#569CD6"), "Icon")));
        template.Triggers.Add(When(TreeViewItem.IsExpandedProperty, false, Set(UIElement.VisibilityProperty, Visibility.Collapsed, "ItemsHost")));
        template.Triggers.Add(When(UIElement.IsMouseOverProperty, true, Set(Border.BackgroundProperty, Brush("#2D2D30"), "Border")));
        template.Triggers.Add(When(TreeViewItem.IsSelectedProperty, true, Set(Border.BackgroundProperty, Brush("#3E3E42"), "Border")));
        template.Triggers.Add(When(UIElement.IsKeyboardFocusedProperty, true, Set(Border.BorderBrushProperty, Brush("#80C7FF"), "Border")));
        var solution = new DataTrigger { Binding = new Binding("IsSolution"), Value = true };
        solution.Setters.Add(Set(TextBlock.TextProperty, "\uE768", "Icon"));
        solution.Setters.Add(Set(TextBlock.ForegroundProperty, Brush("#4EC9B0"), "Icon"));
        template.Triggers.Add(solution);
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private static Style ExpanderStyle()
    {
        var style = new Style(typeof(ToggleButton));
        var icon = Element<TextBlock>("ExpanderIcon");
        icon.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
        icon.SetValue(TextBlock.FontSizeProperty, 10.0);
        icon.SetValue(TextBlock.ForegroundProperty, Brush("#999999"));
        icon.SetValue(TextBlock.TextProperty, "\uE76C");
        icon.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var template = new ControlTemplate(typeof(ToggleButton)) { VisualTree = icon };
        template.Triggers.Add(When(ToggleButton.IsCheckedProperty, true, Set(TextBlock.TextProperty, "\uE70D", "ExpanderIcon")));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private static FrameworkElementFactory TextRun(string text)
    {
        var run = Element<Run>();
        run.SetValue(Run.TextProperty, text);
        return run;
    }
    private static FrameworkElementFactory Element<T>(string? name = null) where T : DependencyObject => new(typeof(T), name);
    private static Setter Set(DependencyProperty property, object value, string target) => new(property, value, target);
    private static Trigger When(DependencyProperty property, object value, params Setter[] setters)
    {
        var trigger = new Trigger { Property = property, Value = value };
        foreach (var setter in setters) trigger.Setters.Add(setter);
        return trigger;
    }
    private static SolidColorBrush Brush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }
}
