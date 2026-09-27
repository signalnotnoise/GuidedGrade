using System.Windows;
using System.Windows.Media;
using UI_Framework;
using UI_Framework.Wpf;

namespace GuidedGrade.Presentation;

internal static class ReviewTheme
{
    internal static readonly ThemeTokens Tokens = new()
    {
        Canvas = "#1B1E23", Surface = "#22262D", Ink = "#EEEEEE", Muted = "#B8C2CC",
        Accent = "#007ACC", AccentHover = "#168ADB", AccentPressed = "#005B99",
        OnAccent = "#FFFFFF", Hover = "#35353B", Pressed = "#414149", Border = "#454D58", Focus = "#80C7FF"
    };

    internal static ViewHost Host(Func<View> body)
    {
        var host = new ViewHost(() =>
        {
            var view = body();
            return view with { ForegroundColor = view.ForegroundColor ?? Tokens.Ink, BackgroundColor = view.BackgroundColor ?? Tokens.Canvas };
        });
        host.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Tokens.Ink));
        host.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Tokens.Canvas));
        ThemeStyles.Apply(host, Tokens);
        ApplyNativeStyles(host);
        return host;
    }

    internal static void ApplyNativeStyles(FrameworkElement element) => NativeTheme.Apply(element);

    internal static void Apply(Window window)
    {
        window.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Tokens.Canvas));
        window.Foreground = Brushes.White;
        ThemeStyles.Apply(window, Tokens);
        ApplyNativeStyles(window);
    }
}
