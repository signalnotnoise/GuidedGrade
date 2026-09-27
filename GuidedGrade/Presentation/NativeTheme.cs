using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace GuidedGrade.Presentation;

/// <summary>Scoped presentation for specialist WPF islands; native controls retain input and automation.</summary>
internal static class NativeTheme
{
    private static readonly object ResourceMarker = new();

    internal static void Apply(FrameworkElement element)
    {
        if (element.Resources.MergedDictionaries.Any(dictionary => dictionary.Contains(ResourceMarker))) return;
        element.Resources.MergedDictionaries.Add(CreateResources());
    }

    private static ResourceDictionary CreateResources()
    {
        var resources = new ResourceDictionary
        {
            [ResourceMarker] = true,
            [SystemColors.WindowBrushKey] = Brush("#22262D"),
            [SystemColors.WindowTextBrushKey] = Brush("#EEEEEE"),
            [SystemColors.ControlBrushKey] = Brush("#22262D"),
            [SystemColors.ControlTextBrushKey] = Brush("#EEEEEE"),
            [typeof(Menu)] = MenuStyle(),
            [typeof(ContextMenu)] = ContextMenuStyle(),
            [typeof(MenuItem)] = MenuItemStyle(),
            [typeof(ScrollBar)] = ScrollBarStyle(),
            ["ScrollThumb"] = ScrollThumbStyle(),
            ["ScrollPage"] = ScrollPageStyle(),
            ["ToolTabStyle"] = TabStyle(typeof(ToggleButton), ToggleButton.IsCheckedProperty, "Content"),
            [typeof(TabItem)] = TabStyle(typeof(TabItem), TabItem.IsSelectedProperty, "Header")
        };
        return resources;
    }

    private static Style MenuStyle()
    {
        var style = new Style(typeof(Menu));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#22262D")));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#EEEEEE")));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private static Style ContextMenuStyle()
    {
        var style = new Style(typeof(ContextMenu));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#22262D")));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#EEEEEE")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#454D58")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        var border = Element<Border>();
        Bind(border, Border.BackgroundProperty, Control.BackgroundProperty);
        Bind(border, Border.BorderBrushProperty, Control.BorderBrushProperty);
        Bind(border, Border.BorderThicknessProperty, Control.BorderThicknessProperty);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.PaddingProperty, new Thickness(4));
        border.AppendChild(ItemsPresenter());
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ContextMenu)) { VisualTree = border }));
        return style;
    }

    private static Style MenuItemStyle()
    {
        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#EEEEEE")));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)));
        var root = Element<Grid>();
        var row = Element<Border>("row");
        Bind(row, Border.BackgroundProperty, Control.BackgroundProperty);
        Bind(row, Border.PaddingProperty, Control.PaddingProperty);
        row.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        var columns = Element<DockPanel>();
        var check = Element<TextBlock>("check");
        check.SetValue(TextBlock.TextProperty, "✓");
        check.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        check.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
        check.SetValue(DockPanel.DockProperty, Dock.Left);
        columns.AppendChild(check);
        var arrow = Element<TextBlock>("arrow");
        arrow.SetValue(TextBlock.TextProperty, "›");
        arrow.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(20, 0, 0, 0));
        arrow.SetValue(DockPanel.DockProperty, Dock.Right);
        columns.AppendChild(arrow);
        columns.AppendChild(Presenter("Header"));
        row.AppendChild(columns);
        root.AppendChild(row);
        var popup = Element<Popup>("PART_Popup");
        popup.SetBinding(Popup.IsOpenProperty, new Binding(nameof(MenuItem.IsSubmenuOpen))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        popup.SetValue(Popup.PlacementProperty, PlacementMode.Right);
        popup.SetValue(Popup.AllowsTransparencyProperty, true);
        popup.SetValue(UIElement.FocusableProperty, false);
        popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Fade);
        var surface = Element<Border>();
        surface.SetValue(Border.BackgroundProperty, Brush("#22262D"));
        surface.SetValue(Border.BorderBrushProperty, Brush("#454D58"));
        surface.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        surface.SetValue(Border.PaddingProperty, new Thickness(4));
        var scroll = Element<ScrollViewer>();
        scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
        scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        scroll.AppendChild(ItemsPresenter());
        surface.AppendChild(scroll);
        popup.AppendChild(surface);
        root.AppendChild(popup);
        var template = new ControlTemplate(typeof(MenuItem)) { VisualTree = root };
        template.Triggers.Add(When(ItemsControl.HasItemsProperty, true, Set(UIElement.VisibilityProperty, Visibility.Visible, "arrow")));
        template.Triggers.Add(When(MenuItem.RoleProperty, MenuItemRole.TopLevelHeader,
            Set(Popup.PlacementProperty, PlacementMode.Bottom, "PART_Popup"), Set(UIElement.VisibilityProperty, Visibility.Collapsed, "arrow")));
        template.Triggers.Add(When(MenuItem.IsHighlightedProperty, true, Set(Border.BackgroundProperty, Brush("#344252"), "row")));
        template.Triggers.Add(When(MenuItem.IsSubmenuOpenProperty, true, Set(Border.BackgroundProperty, Brush("#344252"), "row")));
        template.Triggers.Add(When(MenuItem.IsCheckedProperty, true, Set(UIElement.VisibilityProperty, Visibility.Visible, "check")));
        template.Triggers.Add(When(UIElement.IsEnabledProperty, false, Set(UIElement.OpacityProperty, 0.45)));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    internal static Style ScrollThumbStyle()
    {
        var style = new Style(typeof(Thumb));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#505A68")));
        var border = Element<Border>();
        Bind(border, Border.BackgroundProperty, Control.BackgroundProperty);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Thumb)) { VisualTree = border }));
        style.Triggers.Add(When(UIElement.IsMouseOverProperty, true, Set(Control.BackgroundProperty, Brush("#8393A5"))));
        return style;
    }

    internal static Style ScrollPageStyle()
    {
        var style = new Style(typeof(RepeatButton));
        style.Setters.Add(new Setter(UIElement.FocusableProperty, false));
        var border = Element<Border>();
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(RepeatButton)) { VisualTree = border }));
        return style;
    }

    private static Style ScrollBarStyle()
    {
        var style = new Style(typeof(ScrollBar));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#1B1E23")));
        style.Setters.Add(new Setter(FrameworkElement.WidthProperty, 12.0));
        var border = Element<Border>();
        Bind(border, Border.BackgroundProperty, Control.BackgroundProperty);
        var track = Element<NativeScrollTrack>("PART_Track");
        Bind(track, Track.OrientationProperty, ScrollBar.OrientationProperty);
        border.AppendChild(track);
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ScrollBar)) { VisualTree = border }));
        style.Triggers.Add(When(ScrollBar.OrientationProperty, Orientation.Horizontal,
            Set(FrameworkElement.WidthProperty, double.NaN), Set(FrameworkElement.HeightProperty, 12.0)));
        return style;
    }

    private static Style TabStyle(Type controlType, DependencyProperty selectedProperty, string contentSource)
    {
        var style = new Style(controlType);
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#B8C2CC")));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#22262D")));
        if (controlType == typeof(TabItem)) style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 8, 14, 8)));
        var border = Element<Border>("tab");
        Bind(border, Border.BackgroundProperty, Control.BackgroundProperty);
        Bind(border, Border.PaddingProperty, Control.PaddingProperty);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 2));
        border.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        border.AppendChild(Presenter(contentSource));
        var template = new ControlTemplate(controlType) { VisualTree = border };
        template.Triggers.Add(When(selectedProperty, true,
            Set(Border.BorderBrushProperty, Brush("#50B5FF"), "tab"), Set(Control.ForegroundProperty, Brushes.White)));
        template.Triggers.Add(When(UIElement.IsMouseOverProperty, true, Set(Border.BackgroundProperty, Brush("#344252"), "tab")));
        template.Triggers.Add(When(UIElement.IsKeyboardFocusedProperty, true, Set(Border.BorderBrushProperty, Brushes.White, "tab")));
        template.Triggers.Add(When(UIElement.IsEnabledProperty, false, Set(UIElement.OpacityProperty, 0.45)));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private static FrameworkElementFactory Presenter(string source)
    {
        var presenter = Element<ContentPresenter>();
        presenter.SetValue(ContentPresenter.ContentSourceProperty, source);
        presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        return presenter;
    }

    private static FrameworkElementFactory ItemsPresenter()
    {
        var presenter = Element<ItemsPresenter>();
        presenter.SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Cycle);
        return presenter;
    }

    private static FrameworkElementFactory Element<T>(string? name = null) where T : FrameworkElement => new(typeof(T), name);
    private static void Bind(FrameworkElementFactory element, DependencyProperty target, DependencyProperty source) =>
        element.SetValue(target, new TemplateBindingExtension(source));
    private static Setter Set(DependencyProperty property, object value, string? target = null) => new(property, value, target);
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
