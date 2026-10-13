using System.Windows;

namespace GuidedGrade.ViewModels;

internal sealed class DialogViewportViewModel
{
    internal Rect WorkArea => SystemParameters.WorkArea;
    internal double MaximumWidth => Math.Max(1, WorkArea.Width - 24);
    internal double MaximumHeight => Math.Max(1, WorkArea.Height - 24);
}
