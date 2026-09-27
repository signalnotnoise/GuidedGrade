namespace GuidedGrade.Services;

/// <summary>UI-owned solution scheduler; holds execution, cleanup and grading together.</summary>
internal sealed class AiTestQueue
{
    private readonly LlmJobQueue _jobs = new();
    private readonly HashSet<string> _keys = new(StringComparer.OrdinalIgnoreCase);
    public int Count => _jobs.Count;
    public const int Capacity = LlmJobQueue.Capacity;
    public IReadOnlyList<LlmJobSnapshot> Snapshot() => _jobs.Snapshot();
    public void Cancel(Guid id) => _jobs.Cancel(id);
    public void ClearFinished() => _jobs.ClearFinished();

    public bool TryEnqueue(string key, Func<Task> work, out Task completion)
        => TryEnqueue(key, _ => work(), out completion);

    public bool TryEnqueue(string key, Func<CancellationToken, Task> work, out Task completion)
    {
        completion = Task.CompletedTask;
        if (_keys.Contains(key) || Count >= Capacity) return false;
        _keys.Add(key);
        try
        {
            var task = _jobs.EnqueueAsync(async token => { await work(token); return ""; },
                LlmJobPriority.Assignment, title: key);
            completion = ReleaseKeyAsync(key, task);
            return true;
        }
        catch { _keys.Remove(key); throw; }
    }

    private async Task ReleaseKeyAsync(string key, Task task)
    {
        try { await task; }
        finally { _keys.Remove(key); }
    }
}
