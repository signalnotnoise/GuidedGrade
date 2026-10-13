using System.Windows;
using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.ViewModels;
using GuidedGrade.Views;

namespace GuidedGrade.Presentation;

public abstract class ReviewWindow : Window
{
    private ViewHost? _host;
    private readonly DialogViewportViewModel _viewport = new();
    protected ReviewWindow()
    {
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ReviewTheme.Apply(this);
        Loaded += (_, _) => FitInitialContent();
        Closed += (_, _) => { _host?.Dispose(); _host = null; };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; Close(); }
        };
    }

    protected void ShowView(Func<View> body)
    {
        _host?.Dispose();
        _host = ReviewTheme.Host(body);
        Content = new DialogViewportView(_viewport).Build(_host);
    }

    private void FitInitialContent()
    {
        MaxWidth = _viewport.MaximumWidth;
        MaxHeight = _viewport.MaximumHeight;
        MinWidth = Math.Min(MinWidth, MaxWidth);
        MinHeight = Math.Min(MinHeight, MaxHeight);
        Width = Math.Min(ActualWidth, MaxWidth);
        _host?.Measure(new Size(Math.Max(1, Width - 32), double.PositiveInfinity));
        var needed = (_host?.DesiredSize.Height ?? ActualHeight) + 48;
        if (SizeToContent == SizeToContent.Manual)
            Height = Math.Min(MaxHeight, Math.Max(ActualHeight, needed));
        var area = _viewport.WorkArea;
        Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width));
        Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Math.Min(MaxHeight, Height)));
    }
}
