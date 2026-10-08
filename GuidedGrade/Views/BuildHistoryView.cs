using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GuidedGrade.ViewModels;
using UI_Framework;
using UI_Framework.Wpf;
namespace GuidedGrade.Views;
internal sealed class BuildHistoryView(BuildHistoryViewModel model)
{
    private readonly HistoryChart _chart = new(model);
    internal FrameworkElement Control => _chart;
    internal View Build() => WpfUI.Native(() => _chart).Id("build-history-chart");
    internal void Refresh() => _chart.InvalidateVisual();
    private sealed class HistoryChart(BuildHistoryViewModel model) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(34, 38, 45)), null, new Rect(RenderSize));
            if (model.Points.Count == 0 || ActualWidth < 100) return;
            var ordered = model.Points.OrderBy(p => p.Time).ToArray();
            var start = ordered[0].Time; var end = ordered[^1].Time;
            var span = Math.Max(1, (end - start).TotalSeconds);
            var width = ActualWidth - 32;
            dc.DrawLine(new Pen(Brushes.SlateGray, 1), new Point(16, 32), new Point(ActualWidth - 16, 32));
            foreach (var point in ordered)
            {
                var x = 16 + (point.Time - start).TotalSeconds / span * width;
                dc.DrawLine(new Pen(Brushes.DeepSkyBlue, 2), new Point(x, 16), new Point(x, 43));
            }
            void Label(string text, double x) => dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brushes.WhiteSmoke, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, 48));
            Label(start.ToLocalTime().ToString("g"), 16);
            if (width > 350) Label(end.ToLocalTime().ToString("g"), Math.Max(16, ActualWidth - 165));
        }
        private BuildHistoryPoint? Closest(double x)
        {
            if (model.Points.Count == 0) return null;
            var start = model.Points.Min(p => p.Time); var end = model.Points.Max(p => p.Time);
            var fraction = Math.Clamp((x - 16) / Math.Max(1, ActualWidth - 32), 0, 1);
            return model.Points.MinBy(p => Math.Abs((p.Time - start).TotalSeconds - fraction * Math.Max(1, (end - start).TotalSeconds)));
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); var point = Closest(e.GetPosition(this).X);
            ToolTip = point == null ? null : $"Build {point.Number} | {point.Time.ToLocalTime():G} | {point.FileCount} files | Gap: {(point.Gap == null ? "first build" : point.Gap.Value.ToString())}";
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (Closest(e.GetPosition(this).X) is { } point) model.SelectBuild?.Invoke(point.Number);
        }
    }
}
