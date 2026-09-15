using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Lab_Feedback_WPF.Services;

/// <summary>UI-thread presenter; renders bounded batches instead of dispatching every output chunk.</summary>
internal sealed class RuntimeTerminalPresenter : IDisposable
{
    private readonly RichTextBox _view;
    private readonly DispatcherTimer _timer;
    private Paragraph _paragraph = null!;
    private Paragraph _notice = null!;
    private BoundedConsoleProgress _pending = new();
    internal int RetainedCharacters { get; private set; }
    internal int RetainedRuns { get; private set; }

    public RuntimeTerminalPresenter(RichTextBox view)
    {
        _view = view;
        _view.IsUndoEnabled = false;
        Reset();
        _timer = new DispatcherTimer(DispatcherPriority.Background, view.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public BoundedConsoleProgress BeginSession()
    {
        _pending.Dispose(); // Ignore late output from the previous run.
        _pending = new BoundedConsoleProgress();
        Reset();
        return _pending;
    }

    private void Reset()
    {
        _view.Document.Blocks.Clear();
        _notice = new Paragraph { Margin = new Thickness(0) };
        _paragraph = new Paragraph { Margin = new Thickness(0), LineHeight = 1 };
        _view.Document.Blocks.Add(_notice);
        _view.Document.Blocks.Add(_paragraph);
        RetainedCharacters = RetainedRuns = 0;
    }

    private void OnTick(object? sender, EventArgs e) => Flush();

    public void Flush()
    {
        var chunks = _pending.Drain();
        if (chunks.Count == 0) return;
        _view.BeginChange();
        try
        {
            foreach (var chunk in chunks)
                Add(chunk.Text, chunk.IsStderr ? Brushes.Tomato : chunk.IsInput ? Brushes.LimeGreen
                    : chunk.IsDiagnostic ? Brushes.DeepSkyBlue : Brushes.LightGray);
        }
        finally { _view.EndChange(); }
        _view.ScrollToEnd();
    }

    public void Append(string text, Brush brush)
    {
        Flush();
        Add(text, brush);
        _view.ScrollToEnd();
    }

    private void Add(string text, Brush brush)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (text.Length > OutputLimits.TerminalCharacters)
        {
            text = text[^OutputLimits.TerminalCharacters..];
            MarkTruncated();
        }
        _paragraph.Inlines.Add(new Run(text) { Foreground = brush });
        RetainedCharacters += text.Length;
        RetainedRuns++;
        while (RetainedCharacters > OutputLimits.TerminalCharacters || RetainedRuns > OutputLimits.TerminalRuns)
        {
            var first = (Run)_paragraph.Inlines.FirstInline!;
            RetainedCharacters -= first.Text.Length;
            RetainedRuns--;
            _paragraph.Inlines.Remove(first);
            MarkTruncated();
        }
    }

    private void MarkTruncated()
    {
        if (_notice.Inlines.Count == 0)
            _notice.Inlines.Add(new Run(OutputLimits.TruncationMarker) { Foreground = Brushes.DeepSkyBlue });
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _pending.Dispose();
    }
}
