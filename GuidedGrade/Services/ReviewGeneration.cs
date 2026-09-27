namespace GuidedGrade.Services;

// Accessed on the UI thread; background completions check after dispatching to it.
internal sealed class ReviewGeneration
{
    private readonly Dictionary<string, long> _versions = new(StringComparer.OrdinalIgnoreCase);
    internal long Capture(string? path) => path == null ? 0 : _versions.GetValueOrDefault(path);
    internal bool IsCurrent(string? path, long version) => Capture(path) == version;
    internal void Clear(string path) => _versions[path] = Capture(path) + 1;
}
