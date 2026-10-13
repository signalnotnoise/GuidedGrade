using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.ComponentModel;
using System.Text.RegularExpressions;
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
    internal ICSharpCode.AvalonEdit.TextEditor Source { get; } = new() { IsReadOnly = true, FontFamily = new("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, WordWrap = false };
    internal DocumentSearchViewModel Search { get; }
    internal LogPanelViewModel()
    {
        Search = new(Source, "Log snapshot");
        Source.Document.UndoStack.SizeLimit = 0;
        var background = new SolidColorBrush(Color.FromRgb(27, 30, 35));
        Snapshots.Background = background; Snapshots.Foreground = Brushes.WhiteSmoke;
        Snapshots.BorderBrush = Brushes.DimGray;
        Source.Background = background; Source.Foreground = Brushes.WhiteSmoke;
        Source.TextArea.SelectionBrush = Brushes.SteelBlue;
        GuidedGrade.Presentation.LogSnapshotStyle.Apply(Snapshots);
        Search.Changed += HighlightSnapshots;
        Snapshots.SelectionChanged += (_, _) => Source.Text = (Snapshots.SelectedItem as Entry)?.Snapshot.Content ?? "";
        History.SelectBuild = number =>
        {
            var entry = Snapshots.Items.Cast<Entry>().FirstOrDefault(e => e.Snapshot.BuildNumber == number);
            Snapshots.SelectedItem = entry; if (entry != null) Snapshots.ScrollIntoView(entry);
        };
    }
    private sealed class Entry(FsLogSnapshot snapshot, TimeSpan? gap) : INotifyPropertyChanged
    {
        internal FsLogSnapshot Snapshot { get; } = snapshot;
        internal TimeSpan? Gap { get; } = gap;
        private bool _matchesSearch;
        public bool MatchesSearch
        {
            get => _matchesSearch;
            set { if (_matchesSearch == value) return; _matchesSearch = value; PropertyChanged?.Invoke(this, new(nameof(MatchesSearch))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        public override string ToString() => $"Build {Snapshot.BuildNumber} | {Snapshot.Time.ToLocalTime():G}\n{Snapshot.Name} | {(Gap == null ? "first build" : "gap " + Gap.Value.ToString())}";
    }
    private void HighlightSnapshots()
    {
        var entries = Snapshots.Items.Cast<Entry>().ToArray();
        Regex? pattern = null;
        if (!string.IsNullOrEmpty(Search.ActivePattern))
        {
            var expression = Search.Panel.UseRegex ? Search.ActivePattern : Regex.Escape(Search.ActivePattern);
            if (Search.Panel.WholeWords) expression = @"\b(?:" + expression + @")\b";
            try { pattern = new(expression, Search.Panel.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50)); }
            catch (ArgumentException) { /* The native search panel displays its invalid-pattern message. */ }
        }
        foreach (var entry in entries)
        {
            try { entry.MatchesSearch = pattern != null && (pattern.IsMatch(entry.Snapshot.Content) || pattern.IsMatch(entry.ToString())); }
            catch (RegexMatchTimeoutException) { pattern = null; entry.MatchesSearch = false; }
        }
        if (entries.Any(entry => entry.MatchesSearch) && Snapshots.SelectedItem is not Entry { MatchesSearch: true })
        {
            var first = entries.First(entry => entry.MatchesSearch);
            Snapshots.SelectedItem = first;
            Snapshots.ScrollIntoView(first);
        }
    }
    internal void Clear(string message)
    {
        Search.Close(); Search.Panel.SearchPattern = "";
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
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException)
        { Clear("Cannot read log: " + ex.Message); }
    }
}
