using System.Windows;
using System.Windows.Media;
using UI_Framework;
using UI_Framework.Wpf;

namespace GuidedGrade.Presentation;

internal static class ReviewTheme
{
    private static bool _light = Services.WorkspacePanelPreferences.Load().LightMode;
    private static readonly List<WeakReference<ViewHost>> Hosts = [];
    internal static event Action? Changed;
    private static readonly ThemeTokens Dark = new()
    {
        Canvas = "#1B1E23", Surface = "#22262D", Ink = "#EEEEEE", Muted = "#B8C2CC",
        Accent = "#007ACC", AccentHover = "#168ADB", AccentPressed = "#005B99",
        OnAccent = "#FFFFFF", Hover = "#35353B", Pressed = "#414149", Border = "#454D58", Focus = "#80C7FF"
    };
    private static readonly ThemeTokens Light = new()
    {
        Canvas = "#F5F6F8", Surface = "#FFFFFF", Ink = "#20242A", Muted = "#55616E",
        Accent = "#007ACC", AccentHover = "#168ADB", AccentPressed = "#005B99",
        OnAccent = "#FFFFFF", Hover = "#E3EAF1", Pressed = "#D6E2EF", Border = "#B3BEC9", Focus = "#007ACC"
    };
    internal static ThemeTokens Tokens => _light ? Light : Dark;
    internal static string ColorFor(string value) => !_light ? value : value.ToUpperInvariant() switch
    {
        "#1B1E23" or "#0C0C0C" => Light.Canvas,
        "#22262D" or "#262A32" => Light.Surface,
        "#EEEEEE" or "#F8F8F2" or "#E6EDF5" or "#CCCCCC" => Light.Ink,
        "#B8C2CC" or "#AAB8C8" => Light.Muted,
        "#454D58" => Light.Border,
        "#344252" => Light.Hover,
        _ => value
    };
    internal static void SetLightMode(bool light)
    {
        _light = light;
        Hosts.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in Hosts.ToArray())
        {
            if (!reference.TryGetTarget(out var host) || !host.Dispatcher.CheckAccess()) continue;
            host.Resources.MergedDictionaries.Clear();
            ThemeStyles.Apply(host, Tokens); ApplyNativeStyles(host);
            host.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Tokens.Canvas));
            host.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Tokens.Ink));
            try { host.Refresh(); } catch (ObjectDisposedException) { }
        }
        if (Application.Current != null && Application.Current.Dispatcher.CheckAccess())
            foreach (Window window in Application.Current.Windows) { window.Resources.MergedDictionaries.Clear(); Apply(window); }
        Changed?.Invoke();
    }

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
        Hosts.RemoveAll(reference => !reference.TryGetTarget(out _));
        Hosts.Add(new(host));
        return host;
    }

    internal static void ApplyNativeStyles(FrameworkElement element) => NativeTheme.Apply(element);

    internal static void Apply(Window window)
    {
        window.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Tokens.Canvas));
        window.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Tokens.Ink));
        ThemeStyles.Apply(window, Tokens);
        ApplyNativeStyles(window);
    }
}
