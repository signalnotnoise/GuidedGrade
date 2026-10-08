namespace GuidedGrade.Services;

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
    bool SupportsWindowInput => false;
    Task SendWindowInputAsync(WindowInputAction action, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("This session does not support window events."));
    void CloseInput();
    void Kill();
}
