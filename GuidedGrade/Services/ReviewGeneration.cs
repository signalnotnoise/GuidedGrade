namespace GuidedGrade.Services;

// Evicted captures become stale rather than reviving cleared work.
internal sealed class ReviewGeneration
{
    private readonly BoundedCache<long> _versions = new(4096);
    private long _next;
    internal long Capture(string? path)
    {
        if (path == null) return 0;
        if (_versions.TryGetValue(path, out var version)) return version;
        _versions[path] = ++_next;
        return _next;
    }
    internal bool IsCurrent(string? path, long version) => path == null ? version == 0 : _versions.TryGetValue(path, out var current) && current == version;
    internal void Clear(string path) => _versions[path] = ++_next;
}
