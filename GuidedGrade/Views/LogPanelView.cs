using System.Windows.Controls;
using GuidedGrade.ViewModels;
using UI_Framework;
using UI_Framework.Wpf;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class LogPanelView(LogPanelViewModel model)
{
    private readonly BuildHistoryView _history = new(model.History);
    internal View Build()
    {
        model.Heading.Text = model.Summary.Value;
        model.Panel.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(27, 30, 35));
        foreach (System.Windows.UIElement child in model.Panel.Children) child.InvalidateVisual();
        if (model.Panel.Children.Count > 0) return WpfUI.Native(() => model.Panel).Id("log-panel");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new() { Width = new(360) });
        grid.ColumnDefinitions.Add(new() { Width = new(5) });
        grid.ColumnDefinitions.Add(new() { Width = new(1, System.Windows.GridUnitType.Star) });
        grid.Children.Add(model.Snapshots);
        var splitter = new GridSplitter { Width = 5, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
        Grid.SetColumn(splitter, 1); grid.Children.Add(splitter);
        Grid.SetColumn(model.Source, 2); grid.Children.Add(model.Source);
        var dock = model.Panel;
        var heading = model.Heading;
        DockPanel.SetDock(heading, System.Windows.Controls.Dock.Top); dock.Children.Add(heading);
        var chart = _history.Control; chart.Height = 76;
        DockPanel.SetDock(chart, System.Windows.Controls.Dock.Top); dock.Children.Add(chart); dock.Children.Add(grid);
        return WpfUI.Native(() => dock).Id("log-panel");
    }
}
