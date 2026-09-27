using System.Windows;

namespace GuidedGrade;

public partial class MainWindow
{
    private Services.WorkspacePanelPreferences _panelPreferences = Services.WorkspacePanelPreferences.Load();

    private void ApplyPanelPreferences()
    {
        _showComments.Value = _panelPreferences.ShowComments;
        _showQueue.Value = _panelPreferences.ShowQueue;
        if ((!_panelPreferences.ShowComments && rightPanelTabs.SelectedItem == commentsDetailsTab) ||
            (!_panelPreferences.ShowQueue && rightPanelTabs.SelectedItem == queueDetailsTab)) CloseSidePanel();
        UpdatePinnedTabs();
    }

    private void UpdatePinnedTabs()
    {
        var open = sidePanelColumn.Width.Value > 0;
        _activePanel.Value = open ? rightPanelTabs.SelectedIndex : -1;
    }
}
