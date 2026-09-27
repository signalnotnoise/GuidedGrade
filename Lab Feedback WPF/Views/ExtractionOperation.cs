namespace Lab_Feedback_WPF.Views;

/// <summary>Worker-owned cancellation lifetime; dialog state stays on its dispatcher.</summary>
public sealed class ExtractionOperation : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private bool _disposed;
    public string Id { get; }
    public string Title { get; }
    public int TotalFiles { get; }
    public int Current { get; internal set; }
    public string CurrentFile { get; internal set; } = "Preparing...";
    public bool IsComplete { get; internal set; }
    public bool IsCancelled { get; private set; }
    public bool HasFailed { get; internal set; }
    public CancellationToken CancellationToken { get; }

    public ExtractionOperation(string id, string title, int totalFiles, CancellationTokenSource cts)
    {
        Id = id; Title = title; TotalFiles = Math.Max(0, totalFiles);
        _cts = cts; CancellationToken = cts.Token;
    }

    internal void Cancel()
    {
        if (IsComplete || IsCancelled) return;
        IsCancelled = true;
        CurrentFile = "Cancelled";
        if (!_disposed) _cts.Cancel();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Dispose();
    }
}
