using GuidedGrade.Views;
using GuidedGrade.ViewModels;
using System.Windows;
using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.Presentation;
using static UI_Framework.UI;
using System.Windows.Threading;
using GuidedGrade.Models;
using GuidedGrade.Services;

namespace GuidedGrade;

public partial class MainWindow
{
    private readonly DispatcherTimer _jobQueueTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private QueueRow[] _lastQueueRows = [];
    private readonly State<QueueRow[]> _queueRows = new([]);
    private readonly State<string> _freeFormPrompt = new("");
    private readonly State<string> _selectedQueueKey = new("");
    private readonly State<double> _queueHeight = new(220);
    private ViewHost? _queueHost;

    private View BuildQueueView() => new JobQueueView(new JobQueueViewModel(_queueRows, _selectedQueueKey, _batchProgress, _freeFormPrompt, _queueHeight,
        DesignBatchReview, () => { if (!OpenSavedReviewForSelectedStudent()) MessageBox.Show(this, "Select a student and assignment with a saved review.", "Saved reviews"); else WorkspaceFeedback_Click(this, new()); },
        () => QueueFreeFormJob_Click(this, new()), () => ClearFinishedJobs_Click(this, new()), CancelQueuedJob)).Build();

    private void InitializeJobQueuePanel()
    {
        queueDetailsTab.Content = _queueHost = ReviewTheme.Host(BuildQueueView);
        queueDetailsTab.SizeChanged += (_, _) => _queueHeight.Value = Math.Max(140, queueDetailsTab.ActualHeight * 0.4);
        Closed += (_, _) => { _queueHost?.Dispose(); _queueHost = null; };
        _jobQueueTimer.Tick += (_, _) => RefreshJobQueue();
        _jobQueueTimer.Start();
        Closed += (_, _) => _jobQueueTimer.Stop();
    }

    private void RefreshJobQueue()
    {
        var tests = _aiTestQueue.Snapshot();
        var requests = LlmJobQueue.Shared.Snapshot();
        var testsLeft = tests.Count(job => job.CanCancel || job.Status == "Cancelling");
        var requestsLeft = requests.Count(job => job.CanCancel || job.Status == "Cancelling");
        _queueSummary.Value = $"Queue: {testsLeft} tests / {requestsLeft} AI left";
        var rows = tests.Select(job => new QueueRow(job, true))
            .Concat(requests.Select(job => new QueueRow(job, false)))
            .OrderBy(row => row.Job.Status is "Finished" or "Failed" or "Cancelled").ToArray();
        if (_lastQueueRows.SequenceEqual(rows)) return;
        _lastQueueRows = rows;
        _queueRows.Value = rows;
        if (!rows.Any(row => row.Key == _selectedQueueKey.Value)) _selectedQueueKey.Value = "";
    }

    private void ShowQueue_Click(object sender, RoutedEventArgs e)
    {
        if (!_panelPreferences.ShowQueue) return;
        RefreshJobQueue();
        rightPanelTabs.SelectedItem = queueDetailsTab;
        OpenSidePanel();
    }

    private void HideQueue_Click(object sender, RoutedEventArgs e) => CloseSidePanel();

    private void CancelQueuedJob(QueueRow row)
    {
        if (row.IsSolution) _aiTestQueue.Cancel(row.Job.Id);
        else LlmJobQueue.Shared.Cancel(row.Job.Id);
        RefreshJobQueue();
    }

    private void ClearFinishedJobs_Click(object sender, RoutedEventArgs e)
    {
        _aiTestQueue.ClearFinished();
        LlmJobQueue.Shared.ClearFinished();
        RefreshJobQueue();
    }

    private async void QueueFreeFormJob_Click(object sender, RoutedEventArgs e)
    {
        var prompt = _freeFormPrompt.Value.Trim();
        if (prompt.Length == 0) return;
        try
        {
            var completion = LlmCompletionService.CompleteAsync(LLMSettings.Load(),
                "Complete the user's task and return a clear text response. You cannot execute commands or modify files.",
                prompt, jobTitle: prompt);
            _freeFormPrompt.Value = "";
            RefreshJobQueue();
            await completion;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "LLM job failed");
        }
        finally { RefreshJobQueue(); }
    }

}
