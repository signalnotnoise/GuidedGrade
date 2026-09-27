using GuidedGrade.Presentation;
using UI_Framework;
using UI_Framework.Wpf;
using static UI_Framework.UI;

namespace GuidedGrade.Views;

public sealed class ExtractionProgressDialog : ReviewWindow
{
    private readonly Dictionary<string, ExtractionOperation> _operations = new();
    private readonly CancellationTokenSource _globalCts = new();
    private readonly State<int> _revision = new(0);
    private bool _closed;
    public CancellationToken GlobalCancellationToken { get; }

    public ExtractionProgressDialog()
    {
        GlobalCancellationToken = _globalCts.Token;
        Title = "Extracting files"; Width = 500; Height = 520; MinWidth = 360; MinHeight = 280;
        ShowView(Body);
        Closed += (_, _) =>
        {
            _closed = true;
            foreach (var operation in _operations.Values) operation.Cancel();
            _globalCts.Cancel();
            _globalCts.Dispose();
        };
    }

    private View Body()
    {
        _ = _revision.Value;
        var active = _operations.Values.Count(op => !op.IsComplete && !op.IsCancelled);
        return Scroll(VStack(
            Text(active == 0 ? "No active extractions" : $"{active} active extraction{(active == 1 ? "" : "s")}").FontSize(20),
            VStack(_operations.Values.Select(OperationCard).ToArray()).Spacing(12),
            Button(active == 0 ? "Close" : "Cancel all", Close)
        ).Spacing(16).Padding(20));
    }

    private View OperationCard(ExtractionOperation operation)
    {
        // Snapshot native updates while Body is tracked by the framework.
        var current = operation.Current;
        var maximum = Math.Max(1, operation.TotalFiles);
        return VStack(
            Text(operation.Title).FontSize(15),
            WpfUI.Native(() => new System.Windows.Controls.ProgressBar { Minimum = 0 },
                bar => { bar.Maximum = maximum; bar.Value = current; })
                .Id("progress").Height(8).AccessibilityLabel($"Extraction progress: {operation.Title}"),
            Text($"{current} of {operation.TotalFiles}"),
            Text(operation.CurrentFile).Foreground(operation.HasFailed ? "#FF719D" : ReviewTheme.Tokens.Muted),
            Button(operation.IsCancelled ? "Cancelled" : operation.IsComplete ? "Done" : "Cancel", () =>
            {
                operation.Cancel(); _revision.Value++;
                if (_operations.Values.All(op => op.IsComplete || op.IsCancelled)) Close();
            }).IsEnabled(!operation.IsComplete && !operation.IsCancelled)
        ).Spacing(8).Padding(12).Background(ReviewTheme.Tokens.Surface).Id(operation.Id);
    }

    public ExtractionOperation AddOperation(string operationId, string title, int totalFiles) => Dispatcher.Invoke(() =>
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        if (_operations.ContainsKey(operationId)) throw new ArgumentException("Operation already registered.", nameof(operationId));
        var operation = new ExtractionOperation(operationId, title, totalFiles,
            CancellationTokenSource.CreateLinkedTokenSource(GlobalCancellationToken));
        _operations.Add(operationId, operation); _revision.Value++;
        return operation;
    });

    public void UpdateOperation(string operationId, int current, string fileName) => Dispatcher.Invoke(() =>
    {
        if (_closed || !_operations.TryGetValue(operationId, out var op) || op.IsComplete || op.IsCancelled) return;
        op.Current = Math.Clamp(current, 0, op.TotalFiles); op.CurrentFile = fileName; _revision.Value++;
    });

    public void CompleteOperation(string operationId) => Dispatcher.Invoke(() =>
    {
        if (_closed || !_operations.TryGetValue(operationId, out var op) || op.IsComplete || op.IsCancelled) return;
        op.Current = op.TotalFiles; op.CurrentFile = "Complete"; op.IsComplete = true; _revision.Value++;
        if (_operations.Values.All(item => item.IsComplete || item.IsCancelled)) Close();
    });

    public void FailOperation(string operationId, string message) => Dispatcher.Invoke(() =>
    {
        if (_closed || !_operations.TryGetValue(operationId, out var op) || op.IsComplete || op.IsCancelled) return;
        op.CurrentFile = "Error: " + message; op.HasFailed = true; op.IsComplete = true; _revision.Value++;
    });
}
