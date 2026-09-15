namespace Lab_Feedback_WPF.Services;

/// <summary>Bounded producer mailbox; Report never posts work to the UI dispatcher.</summary>
internal sealed class BoundedConsoleProgress : IProgress<ConsoleProgress>, IDisposable
{
    private readonly object _gate = new();
    private readonly Queue<ConsoleProgress> _chunks = new();
    private int _characters;
    private bool _dropped;
    private bool _disposed;

    public void Report(ConsoleProgress value)
    {
        if (string.IsNullOrEmpty(value.Text)) return;
        lock (_gate)
        {
            if (_disposed) return;
            var text = value.Text;
            if (text.Length > OutputLimits.PendingTerminalCharacters)
            {
                text = text[^OutputLimits.PendingTerminalCharacters..];
                _dropped = true;
            }
            while (_chunks.Count > 0 && (_characters + text.Length > OutputLimits.PendingTerminalCharacters
                || _chunks.Count >= OutputLimits.PendingTerminalChunks))
            {
                _characters -= _chunks.Dequeue().Text.Length;
                _dropped = true;
            }
            _chunks.Enqueue(new ConsoleProgress
            {
                Text = text, IsInput = value.IsInput, IsStderr = value.IsStderr, IsDiagnostic = value.IsDiagnostic
            });
            _characters += text.Length;
        }
    }

    public IReadOnlyList<ConsoleProgress> Drain()
    {
        lock (_gate)
        {
            var result = new List<ConsoleProgress>(_chunks.Count + 1);
            if (_dropped) result.Add(new ConsoleProgress { Text = OutputLimits.TruncationMarker, IsDiagnostic = true });
            while (_chunks.TryDequeue(out var chunk)) result.Add(chunk);
            _characters = 0;
            _dropped = false;
            return result;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _chunks.Clear();
            _characters = 0;
            _dropped = false;
        }
    }
}
