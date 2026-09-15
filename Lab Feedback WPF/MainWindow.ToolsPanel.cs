using System.Windows;

namespace Lab_Feedback_WPF;

public partial class MainWindow
{
    private bool _violationsToolSelected;

    private void ConsoleToolTab_Click(object sender, RoutedEventArgs e) => SelectToolsPanel(false, true);
    private void ViolationsToolTab_Click(object sender, RoutedEventArgs e) => SelectToolsPanel(true, true);
    private void ToggleToolsPanel_Click(object sender, RoutedEventArgs e)
        => SetToolsPanelVisible(toolsPanelContent.Visibility != Visibility.Visible);

    private void SelectToolsPanel(bool violations, bool toggleSelected = false)
    {
        var collapse = toggleSelected && violations == _violationsToolSelected && toolsPanelContent.Visibility == Visibility.Visible;
        _violationsToolSelected = violations;
        consoleToolTab.IsChecked = !violations;
        violationsToolTab.IsChecked = violations;
        SetToolsPanelVisible(!collapse);
    }

    private void SetToolsPanelVisible(bool visible)
    {
        toolsPanelRow.Height = visible ? new GridLength(0.65, GridUnitType.Star) : new GridLength(0);
        toolsPanelContent.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        violationsPanel.Visibility = visible && _violationsToolSelected ? Visibility.Visible : Visibility.Collapsed;
        runtimeTerminalPanel.Visibility = visible && !_violationsToolSelected ? Visibility.Visible : Visibility.Collapsed;
        runtimeTerminalPanel.Tag = visible && !_violationsToolSelected ? "open" : "closed";
        toolsPanelToggle.Content = visible ? "Hide panel" : "Show panel";
    }
}
