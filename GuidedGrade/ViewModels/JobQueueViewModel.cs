using UI_Framework;
using GuidedGrade.Services;
namespace GuidedGrade.ViewModels;
internal sealed class JobQueueViewModel(State<QueueRow[]> rows, State<string> selectedKey, State<string> progress, State<string> prompt, State<double> height, Action batch, Action openReview, Action queueTask, Action clearFinished, Action<QueueRow> cancel)
{
 internal State<QueueRow[]> Rows { get; } = rows;
 internal State<string> SelectedKey { get; } = selectedKey;
 internal State<string> Progress { get; } = progress;
 internal State<string> Prompt { get; } = prompt;
 internal State<double> Height { get; } = height;
 internal Action Batch { get; } = batch;
 internal Action OpenReview { get; } = openReview;
 internal Action QueueTask { get; } = queueTask;
 internal Action ClearFinished { get; } = clearFinished;
 internal Action<QueueRow> Cancel { get; } = cancel;
}
    internal sealed record QueueRow(LlmJobSnapshot Job, bool IsSolution)
    {
        public string Key => (IsSolution ? "solution:" : "request:") + Job.Id;
        public string Title => Job.Title;
        public string Description => $"{(IsSolution ? "Solution test" : "LLM request")} · {Job.Priority} · {Job.Status}";
        public bool CanCancel => Job.CanCancel;
    }
