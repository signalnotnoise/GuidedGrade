namespace GuidedGrade.Services;

public enum LlmJobPriority { Assignment, General }
public sealed record LlmJobSnapshot(Guid Id, string Title, LlmJobPriority Priority, string Status, string Result, bool CanCancel);

/// <summary>Thread-safe, bounded model scheduler. Priority applies between requests, never mid-request.</summary>
public sealed class LlmJobQueue
{
    public static LlmJobQueue Shared { get; } = new();
    public const int Capacity = 50;
    private readonly object _gate = new();
    private readonly List<Job> _pending = new();
    private readonly List<LlmJobSnapshot> _history = new();
    private Job? _active;
    private bool _running;
    public int Count { get { lock (_gate) return _pending.Count + (_active == null ? 0 : 1); } }

    public IReadOnlyList<LlmJobSnapshot> Snapshot()
    {
        lock (_gate)
            return (_active == null ? Enumerable.Empty<Job>() : new[] { _active })
                .Concat(_pending.OrderBy(job => job.Priority))
                .Select(job => new LlmJobSnapshot(job.Id, job.Title, job.Priority, job.Status, job.Result,
                    job.Status is "Waiting" or "Running")).Concat(_history.AsEnumerable().Reverse()).ToArray();
    }

    public void ClearFinished() { lock (_gate) _history.Clear(); }

    public Task<string> EnqueueAsync(Func<CancellationToken, Task<string>> work,
        LlmJobPriority priority = LlmJobPriority.General, CancellationToken cancellationToken = default,
        string title = "LLM request")
    {
        ArgumentNullException.ThrowIfNull(work);
        if (priority is not (LlmJobPriority.Assignment or LlmJobPriority.General))
            throw new ArgumentOutOfRangeException(nameof(priority));
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<string>(cancellationToken);
        bool start;
        Job job;
        lock (_gate)
        {
            if (Count >= Capacity) throw new InvalidOperationException("The queue has reached its 50-job limit. Try again after a job finishes.");
            job = new Job(work, priority, cancellationToken, title);
            _pending.Add(job);
            start = !_running;
            _running = true;
        }
        var registration = cancellationToken.Register(() => Cancel(job.Id));
        if (start) _ = DrainAsync();
        return AwaitCompletionAsync(job.Completion.Task, registration);
    }

    private static async Task<string> AwaitCompletionAsync(Task<string> completion, CancellationTokenRegistration registration)
    {
        try { return await completion.ConfigureAwait(false); }
        finally { await registration.DisposeAsync().ConfigureAwait(false); }
    }

    public void Cancel(Guid id)
    {
        lock (_gate)
        {
            var job = _active?.Id == id ? _active : _pending.FirstOrDefault(item => item.Id == id);
            if (job == null) return;
            job.Status = "Cancelling";
            job.Cancellation.Cancel();
            if (_pending.Remove(job))
            {
                job.Completion.TrySetCanceled(job.Cancellation.Token);
                Finish(job, "Cancelled", "");
            }
        }
    }

    private void Finish(Job job, string status, string result)
    {
        job.Status = status;
        job.Result = result.Length <= 32000 ? result : result[..32000] + "\n[Display truncated]";
        job.Work = null;
        job.Cancellation.Dispose();
        _history.Add(new LlmJobSnapshot(job.Id, job.Title, job.Priority, status, job.Result, false));
        if (_history.Count > 50) _history.RemoveAt(0);
    }

    private async Task DrainAsync()
    {
        while (true)
        {
            Job job;
            lock (_gate)
            {
                if (_pending.Count == 0) { _running = false; return; }
                job = _pending.FirstOrDefault(item => item.Priority == LlmJobPriority.Assignment) ?? _pending[0];
                _pending.Remove(job);
                _active = job;
                job.Status = "Running";
            }
            string result = "";
            Exception? error = null;
            var token = job.Cancellation.Token;
            try
            {
                token.ThrowIfCancellationRequested();
                result = await job.Work!(token);
                token.ThrowIfCancellationRequested();
            }
            catch (Exception ex) { error = ex; }
            lock (_gate)
            {
                _active = null;
                if (token.IsCancellationRequested)
                {
                    job.Completion.TrySetCanceled(token);
                    Finish(job, "Cancelled", "");
                }
                else if (error != null)
                {
                    job.Completion.TrySetException(error);
                    Finish(job, "Failed", error.Message);
                }
                else
                {
                    job.Completion.TrySetResult(result);
                    Finish(job, "Finished", result);
                }
            }
        }
    }

    private sealed class Job(Func<CancellationToken, Task<string>> work, LlmJobPriority priority, CancellationToken token, string title)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Title { get; } = title[..Math.Min(title.Length, 160)];
        public LlmJobPriority Priority { get; } = priority;
        public string Status { get; set; } = "Waiting";
        public string Result { get; set; } = "";
        public Func<CancellationToken, Task<string>>? Work { get; set; } = work;
        public CancellationTokenSource Cancellation { get; } = CancellationTokenSource.CreateLinkedTokenSource(token);
        public TaskCompletionSource<string> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
