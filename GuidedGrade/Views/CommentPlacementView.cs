using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using UI_Framework;
using UI_Framework.Wpf;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class CommentPlacementView(CommentPlacementViewModel model)
{
    internal View Build() => HStack(WpfUI.Native(() =>
    {
        var handle = new Button { Content = "↕", Style = NativeTheme.NavigationTabStyle(), ToolTip = model.Hint,
            IsEnabled = model.Enabled && !model.Pinned, Cursor = Cursors.SizeAll, MinWidth = 28, MinHeight = 28 };
        AutomationProperties.SetName(handle, "Drag inline comment");
        Point? start = null;
        handle.PreviewMouseLeftButtonDown += (_, e) => start = e.GetPosition(handle);
        handle.PreviewMouseLeftButtonUp += (_, _) => start = null;
        handle.PreviewMouseMove += (_, e) =>
        {
            if (start is not Point point || e.LeftButton != MouseButtonState.Pressed) return;
            var current = e.GetPosition(handle);
            if (Math.Abs(current.X - point.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - point.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            start = null; model.Drag(handle);
        };
        return handle;
    }).Id("drag-handle"),
        new ToolbarActionView(new ToolbarActionViewModel(model.Pinned ? "Unpin" : "Pin", model.TogglePin,
            "Pin inline comment", model.Enabled, Hint: model.Pinned ? "Unlock this comment’s code location." : "Lock and save this comment’s code location.")).Build()
    ).Spacing(4);
}
