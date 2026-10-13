using System.Windows;
using System.Windows.Controls;
using GuidedGrade.ViewModels;

namespace GuidedGrade.Views;

// Native scrolling adapter around the pinned-framework dialog content.
internal sealed class DialogViewportView(DialogViewportViewModel model)
{
    internal ScrollViewer Build(UIElement content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        // Horizontal scrolling measures content at infinity, allowing pickers and
        // their popups to retain widths far beyond the dialog's client area.
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        CanContentScroll = false,
        MaxWidth = model.MaximumWidth,
        MaxHeight = model.MaximumHeight,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Padding = new Thickness(4)
    };
}
