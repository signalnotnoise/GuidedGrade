using System.Windows;

namespace GuidedGrade;

public partial class MainWindow
{
    private bool _violationsToolSelected;

    private void ConsoleToolTab_Click(object sender, RoutedEventArgs e) => SelectToolsPanel(false, true);
    private void ViolationsToolTab_Click(object sender, RoutedEventArgs e) => SelectToolsPanel(true, true);
    private void ToggleToolsPanel_Click(object sender, RoutedEventArgs e)
        => SetToolsPanelVisible(toolsPanelContent.Visibility != Visibility.Visible);

    private void SelectToolsPanel(bool violations, bool toggleSelected = false)
    {
        var collapse = !_logSelected.Value && toggleSelected && violations == _violationsToolSelected && toolsPanelContent.Visibility == Visibility.Visible;
        _logSelected.Value = false;
        _violationsToolSelected = violations;
        _violationsSelected.Value = violations;
        SetToolsPanelVisible(!collapse);
    }

    private void SetToolsPanelVisible(bool visible)
    {
        toolsPanelRow.Height = visible ? new GridLength(0.65, GridUnitType.Star) : new GridLength(0);
        toolsPanelContent.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        violationsPanel.Visibility = visible && !_logSelected.Value && _violationsToolSelected ? Visibility.Visible : Visibility.Collapsed;
        runtimeTerminalPanel.Visibility = visible && !_logSelected.Value && !_violationsToolSelected ? Visibility.Visible : Visibility.Collapsed;
        runtimeTerminalPanel.Tag = visible && !_logSelected.Value && !_violationsToolSelected ? "open" : "closed";
        _logPanel.Visibility = visible && _logSelected.Value ? Visibility.Visible : Visibility.Collapsed;
        _toolsVisible.Value = visible;
    }
}
