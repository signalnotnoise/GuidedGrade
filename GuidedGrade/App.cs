using System.Diagnostics;
using System.IO;
using System.Windows;
using GuidedGrade.Services;
using UI_Framework.Wpf;

namespace GuidedGrade;

public sealed class App : Application
{
    private WpfComCleanupPolicy? cleanupPolicy;
    internal bool CleanupEnabled => cleanupPolicy is not null;
    internal int CleanupFailureCount => cleanupPolicy?.FailureCount ?? 0;

    [STAThread]
    public static int Main(string[] args)
    {
        var application = new App { ShutdownMode = ShutdownMode.OnMainWindowClose };
        application.ConfigureCleanup();
        return application.Run(new MainWindow());
    }

    internal void ConfigureCleanup()
    {
        Dispatcher.VerifyAccess();
        if (cleanupPolicy is not null) throw new InvalidOperationException("Cleanup policy already installed.");
        cleanupPolicy = new WpfComCleanupPolicy(Dispatcher, ReportCleanupFailure);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // MainWindow.Closed disposes shell, grading, panel and terminal owners first.
        // The UI thread's CLR setting cannot be restored; this owner ends with the app.
        try { cleanupPolicy?.CloseBeforeDispatcherShutdown(); }
        catch (Exception error) { ReportCleanupFailure(error); e.ApplicationExitCode = 1; }
        if (CleanupFailureCount != 0) e.ApplicationExitCode = 1;
        base.OnExit(e);
    }

    private static void ReportCleanupFailure(Exception error)
    {
        // Never open a modal dialog or throw from a COM cleanup callback.
        var message = $"{DateTimeOffset.UtcNow:O} Application COM cleanup failed: {error}";
        Trace.WriteLine(message);
        try
        {
            var folder = Path.Combine(AppDataPaths.LocalDirectory, "Diagnostics");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "com-cleanup.log"), message + Environment.NewLine);
        }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"Unable to save COM cleanup diagnostics: {logError.Message}");
        }
    }
}
