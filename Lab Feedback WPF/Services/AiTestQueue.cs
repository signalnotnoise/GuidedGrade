namespace Lab_Feedback_WPF.Services;

/// <summary>FIFO queue owned by the UI thread. Jobs include execution, disposal and grading.</summary>
internal sealed class AiTestQueue
{
    private readonly Queue<(string Key, Func<Task> Work, TaskCompletionSource Completion)> _pending = new();
    private readonly HashSet<string> _keys = new(StringComparer.OrdinalIgnoreCase);
    private bool _running;
    public int Count => _keys.Count;
    public const int Capacity = 50;

    public bool TryEnqueue(string key, Func<Task> work, out Task completion)
    {
        completion = Task.CompletedTask;
        if (_keys.Contains(key) || Count >= Capacity) return false;
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _keys.Add(key);
        _pending.Enqueue((key, work, source));
        completion = source.Task;
        if (!_running) _ = DrainAsync();
        return true;
    }

    private async Task DrainAsync()
    {
        _running = true;
        while (_pending.TryDequeue(out var job))
        {
            Exception? failure = null;
            try { await job.Work(); }
            catch (Exception ex) { failure = ex; }
            finally { _keys.Remove(job.Key); }
            if (failure == null) job.Completion.SetResult();
            else job.Completion.SetException(failure);
        }
        _running = false;
    }
}
