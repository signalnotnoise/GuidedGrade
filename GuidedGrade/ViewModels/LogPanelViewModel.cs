using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using GuidedGrade.Services;
using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class LogPanelViewModel
{
    internal DockPanel Panel { get; } = new();
    internal TextBlock Heading { get; } = new() { Margin = new(8), Foreground = Brushes.WhiteSmoke, TextWrapping = TextWrapping.Wrap };
    internal State<string> Summary { get; } = new("Open a .fslog to inspect source history locally.");
    internal BuildHistoryViewModel History { get; } = new();
    internal State<bool> IsLoaded { get; } = new(false);
    internal State<int> RecordedBuildCount { get; } = new(0);
    internal int BuildCount => RecordedBuildCount.Value;
    internal ListBox Snapshots { get; } = new();
    internal TextBox Source { get; } = new() { IsReadOnly = true, AcceptsReturn = true, FontFamily = new("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap, IsUndoEnabled = false };
    internal LogPanelViewModel()
    {
        var background = new SolidColorBrush(Color.FromRgb(27, 30, 35));
        Snapshots.Background = background; Snapshots.Foreground = Brushes.WhiteSmoke;
        Snapshots.BorderBrush = Brushes.DimGray;
        Source.Background = background; Source.Foreground = Brushes.WhiteSmoke; Source.CaretBrush = Brushes.WhiteSmoke;
        Source.SelectionBrush = Brushes.SteelBlue; Source.SelectionOpacity = 0.6;
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.WhiteSmoke));
        style.Setters.Add(new Setter(Control.BackgroundProperty, background));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));
        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 90, 140))));
        selected.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        style.Triggers.Add(selected); Snapshots.ItemContainerStyle = style;
        Snapshots.SelectionChanged += (_, _) => Source.Text = (Snapshots.SelectedItem as Entry)?.Snapshot.Content ?? "";
        History.SelectBuild = number =>
        {
            var entry = Snapshots.Items.Cast<Entry>().FirstOrDefault(e => e.Snapshot.BuildNumber == number);
            Snapshots.SelectedItem = entry; if (entry != null) Snapshots.ScrollIntoView(entry);
        };
    }
    private sealed record Entry(FsLogSnapshot Snapshot, TimeSpan? Gap)
    {
        public override string ToString() => $"Build {Snapshot.BuildNumber} | {Snapshot.Time.ToLocalTime():G}\n{Snapshot.Name} | {(Gap == null ? "first build" : "gap " + Gap.Value.ToString())}";
    }
    internal void Clear(string message)
    {
        Snapshots.ItemsSource = null; Source.Clear(); History.Points = []; RecordedBuildCount.Value = 0; IsLoaded.Value = false; Summary.Value = message;
    }
    internal void Load(string path)
    {
        Clear("Loading log…");
        try
        {
            var log = FsLogReader.Read(path);
            var groups = log.Snapshots.GroupBy(s => s.BuildNumber).OrderBy(g => g.Key).ToArray();
            var points = new List<BuildHistoryPoint>();
            DateTimeOffset? previous = null;
            foreach (var group in groups)
            {
                var time = group.First().Time;
                points.Add(new(group.Key, time, previous == null ? null : time - previous.Value, group.Count())); previous = time;
            }
            History.Points = points; RecordedBuildCount.Value = log.BuildCount; IsLoaded.Value = true;
            var gaps = points.ToDictionary(p => p.Number, p => p.Gap);
            Snapshots.ItemsSource = log.Snapshots.OrderByDescending(s => s.BuildNumber).Select(s => new Entry(s, gaps[s.BuildNumber])).ToArray();
            Snapshots.SelectedIndex = 0;
            var elapsed = points.Count < 2 ? TimeSpan.Zero : points.Max(p => p.Time) - points.Min(p => p.Time);
            var largest = points.Where(p => p.Gap.HasValue).Select(p => p.Gap!.Value).DefaultIfEmpty(TimeSpan.Zero).Max();
            Summary.Value = $"{Path.GetFileName(path)} | {BuildCount} builds | {log.Snapshots.Count} file snapshots | elapsed {elapsed} | largest gap {largest}\nEach blue mark is a recorded build. Hover for details; click to inspect its source. Elapsed time includes breaks and is not active work time.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OverflowException)
        { Clear("Cannot read log: " + ex.Message); }
    }
}
