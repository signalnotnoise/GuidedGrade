namespace GuidedGrade.Services;

/// <summary>Fixed-storage tail buffer. Callers synchronize access. Cursors count all received characters.</summary>
internal sealed class BoundedTextBuffer
{
    private readonly char[] _buffer;
    private int _next;
    public long Written { get; private set; }
    public int Length { get; private set; }

    public BoundedTextBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _buffer = new char[capacity];
    }

    public void Append(ReadOnlySpan<char> text)
    {
        Written += text.Length;
        if (text.Length >= _buffer.Length)
        {
            text[^_buffer.Length..].CopyTo(_buffer);
            _next = 0;
            Length = _buffer.Length;
            return;
        }
        var first = Math.Min(text.Length, _buffer.Length - _next);
        text[..first].CopyTo(_buffer.AsSpan(_next));
        text[first..].CopyTo(_buffer);
        _next = (_next + text.Length) % _buffer.Length;
        Length = Math.Min(_buffer.Length, Length + text.Length);
    }

    public string ReadFrom(long from)
    {
        var oldest = Written - Length;
        var dropped = from < oldest;
        from = Math.Clamp(from, oldest, Written);
        var count = (int)(Written - from);
        var start = (_next - count + _buffer.Length) % _buffer.Length;
        var prefix = dropped ? OutputLimits.TruncationMarker : string.Empty;
        return string.Create(prefix.Length + count, (Self: this, start, count, prefix), static (target, state) =>
        {
            state.prefix.AsSpan().CopyTo(target);
            target = target[state.prefix.Length..];
            var first = Math.Min(state.count, state.Self._buffer.Length - state.start);
            state.Self._buffer.AsSpan(state.start, first).CopyTo(target);
            state.Self._buffer.AsSpan(0, state.count - first).CopyTo(target[first..]);
        });
    }

    public override string ToString() => ReadFrom(0);
}
