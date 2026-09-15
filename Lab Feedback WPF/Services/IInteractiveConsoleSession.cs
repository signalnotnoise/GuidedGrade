namespace Lab_Feedback_WPF.Services;

public interface IInteractiveConsoleSession : IAsyncDisposable
{
    bool Started { get; }
    string? Error { get; }
    bool HasExited { get; }
    int? ExitCode { get; }
    string StandardOutput { get; }
    string StandardError { get; }
    Task<ConsoleSlice> WaitForIdleAsync(TimeSpan idle, TimeSpan window, CancellationToken cancellationToken = default);
    Task WriteInputAsync(string text, CancellationToken cancellationToken = default);
    void CloseInput();
    void Kill();
}
