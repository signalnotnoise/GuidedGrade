using System.Windows;
using UI_Framework;
using UI_Framework.Wpf;

namespace Lab_Feedback_WPF.Presentation;

public abstract class ReviewWindow : Window
{
    private ViewHost? _host;
    protected ReviewWindow()
    {
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ReviewTheme.Apply(this);
        Closed += (_, _) => { _host?.Dispose(); _host = null; };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; Close(); }
        };
    }

    protected void ShowView(Func<View> body)
    {
        _host?.Dispose();
        Content = _host = ReviewTheme.Host(body);
    }
}
