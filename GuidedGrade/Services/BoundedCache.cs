namespace GuidedGrade.Services;
internal sealed class BoundedCache<T>(int capacity, Func<string?>? pinned = null)
{
    private readonly int _capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    private readonly Dictionary<string,T> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _order = new();
    internal IEnumerable<string> Keys => _values.Keys;
    internal int Count => _values.Count;
    internal T this[string key]
    {
        get { var value = _values[key]; Touch(key); return value; }
        set
        {
            _values[key] = value; Touch(key);
            while (_values.Count > _capacity)
            {
                var candidate = _order.First!;
                if (string.Equals(candidate.Value, pinned?.Invoke(), StringComparison.OrdinalIgnoreCase)) { _order.RemoveFirst(); _order.AddLast(candidate); continue; }
                _values.Remove(candidate.Value); _order.RemoveFirst();
            }
        }
    }
    private void Touch(string key) { RemoveOrder(key); _order.AddLast(key); }
    private void RemoveOrder(string key) { var node = _order.First; while (node != null) { var next = node.Next; if (string.Equals(node.Value, key, StringComparison.OrdinalIgnoreCase)) _order.Remove(node); node = next; } }
    internal bool TryGetValue(string key, out T value) { if (!_values.TryGetValue(key, out value!)) return false; Touch(key); return true; }
    internal bool Remove(string key) { RemoveOrder(key); return _values.Remove(key); }
    internal void Clear() { _values.Clear(); _order.Clear(); }
}
