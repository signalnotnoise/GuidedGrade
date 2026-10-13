using System.Windows;
using System.Windows.Media;
using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;

namespace GuidedGrade.Views;

// Vector glyph adapter: the pinned package does not provide a panel icon primitive.
internal sealed class PanelIconView(PanelIconViewModel model)
{
    internal FrameworkElement Build() => new PanelIcon(model);

    private sealed class PanelIcon(PanelIconViewModel model) : FrameworkElement
    {
        protected override Size MeasureOverride(Size availableSize) => new(20, 20);

        protected override void OnRender(DrawingContext drawing)
        {
            var ink = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ReviewTheme.Tokens.Muted));
            var accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ReviewTheme.Tokens.Accent));
            var stroke = new Pen(ink, 1.15) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            drawing.DrawRoundedRectangle(null, stroke, new Rect(2.5, 3.5, 15, 13), 3, 3);
            var divider = model.Left ? 7.5 : 12.5;
            if (model.Expanded)
            {
                accent.Opacity = 0.10;
                drawing.DrawRoundedRectangle(accent, null,
                    new Rect(model.Left ? 3.5 : 13, 4.5, 3.5, 11), 2, 2);
            }
            drawing.DrawLine(stroke, new Point(divider, 4), new Point(divider, 16));
        }
    }
}
