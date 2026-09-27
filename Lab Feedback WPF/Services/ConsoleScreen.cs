using System.Text;

namespace Lab_Feedback_WPF.Services;

/// <summary>Bounded text screen for the runner's 120x30 console. Interprets common VT cursor/erase controls.</summary>
internal sealed class ConsoleScreen
{
    private const int Width = 120, Height = 30;
    private readonly char[] _cells = new char[Width * Height];
    private readonly StringBuilder _control = new();
    private int _row, _column, _savedRow, _savedColumn;
    private int _state; // 0=text, 1=ESC, 2=CSI, 3=OSC/string, 4=string ESC, 5=charset
    public bool Incomplete { get; private set; }

    public ConsoleScreen() => Array.Fill(_cells, ' ');

    public void Feed(string text)
    {
        if (text.StartsWith(OutputLimits.TruncationMarker, StringComparison.Ordinal))
        {
            Array.Fill(_cells, ' ');
            _row = _column = _state = 0;
            _control.Clear();
            Incomplete = true;
            text = text[OutputLimits.TruncationMarker.Length..];
        }
        foreach (var c in text)
        {
            if (_state == 3) { if (c == '\a') _state = 0; else if (c == '\u001b') _state = 4; continue; }
            if (_state == 4) { _state = c == '\\' ? 0 : 3; continue; }
            if (_state == 5) { _state = 0; continue; }
            if (_state == 1)
            {
                _state = 0;
                if (c == '[') { _state = 2; _control.Clear(); }
                else if (c is ']' or 'P' or '^' or '_') _state = 3;
                else if (c is '(' or ')') _state = 5;
                else if (c == '7') { _savedRow = _row; _savedColumn = _column; }
                else if (c == '8') { _row = _savedRow; _column = _savedColumn; }
                else if (c == 'c') { Array.Fill(_cells, ' '); _row = _column = 0; }
                continue;
            }
            if (_state == 2)
            {
                if (c is >= '@' and <= '~') { Apply(c); _state = 0; }
                else if (_control.Length < 64) _control.Append(c);
                else { _control.Clear(); _state = 0; Incomplete = true; }
                continue;
            }
            if (c == '\u001b') { _state = 1; continue; }
            if (c == '\r') { _column = 0; continue; }
            if (c == '\n') { NewLine(); continue; }
            if (c == '\b') { _column = Math.Max(0, _column - 1); continue; }
            if (c == '\t') { _column = Math.Min(Width - 1, (_column / 8 + 1) * 8); continue; }
            if (char.IsControl(c)) continue;
            if (_column >= Width) NewLine();
            _cells[_row * Width + _column++] = c;
        }
    }

    private void NewLine()
    {
        _column = 0;
        if (++_row < Height) return;
        Array.Copy(_cells, Width, _cells, 0, Width * (Height - 1));
        Array.Fill(_cells, ' ', Width * (Height - 1), Width);
        _row = Height - 1;
    }

    private void Apply(char command)
    {
        var parameters = _control.ToString();
        if (parameters.StartsWith('?')) return; // Modes such as cursor visibility do not print text.
        var args = parameters.Split(';');
        int P(int index, int fallback = 1) => index < args.Length && int.TryParse(args[index], out var n)
            ? Math.Clamp(n, fallback == 1 ? 1 : 0, 10000) : fallback;
        _column = Math.Min(_column, Width - 1);
        switch (command)
        {
            case 'H': case 'f': _row = Math.Min(Height - 1, P(0) - 1); _column = Math.Min(Width - 1, P(1) - 1); break;
            case 'A': _row = Math.Max(0, _row - P(0)); break;
            case 'B': _row = Math.Min(Height - 1, _row + P(0)); break;
            case 'C': _column = Math.Min(Width - 1, _column + P(0)); break;
            case 'D': _column = Math.Max(0, _column - P(0)); break;
            case 'E': _row = Math.Min(Height - 1, _row + P(0)); _column = 0; break;
            case 'F': _row = Math.Max(0, _row - P(0)); _column = 0; break;
            case 'G': _column = Math.Min(Width - 1, P(0) - 1); break;
            case 'd': _row = Math.Min(Height - 1, P(0) - 1); break;
            case 's': _savedRow = _row; _savedColumn = _column; break;
            case 'u': _row = _savedRow; _column = _savedColumn; break;
            case 'J':
                var offset = _row * Width + _column;
                if (P(0, 0) is 2 or 3) { Array.Fill(_cells, ' '); Incomplete = false; }
                else if (P(0, 0) == 1) Array.Fill(_cells, ' ', 0, offset + 1);
                else Array.Fill(_cells, ' ', offset, _cells.Length - offset);
                break;
            case 'K':
                var first = P(0, 0) is 1 or 2 ? 0 : _column;
                var last = P(0, 0) is 0 or 2 ? Width : _column + 1;
                Array.Fill(_cells, ' ', _row * Width + first, last - first);
                break;
            // SGR colors and unsupported non-text controls are intentionally not emitted.
        }
    }

    public string Snapshot()
    {
        var lines = Enumerable.Range(0, Height).Select(r => new string(_cells, r * Width, Width).TrimEnd()).ToArray();
        return string.Join('\n', lines).TrimEnd();
    }

    public string CursorContext()
    {
        var nearCursor = Enumerable.Range(Math.Max(0, _row - 7), Math.Min(8, _row + 1))
            .Select(r => new string(_cells, r * Width, Width).TrimEnd());
        return string.Join('\n', nearCursor);
    }

    public string MenuContext() => string.Join('\n', Enumerable.Range(0, _row + 1)
        .Select(r => new string(_cells, r * Width, Width).TrimEnd()));

    public string PromptContext()
    {
        return (Incomplete ? "[Some earlier terminal updates were lost; screen may be incomplete.]\n" : "")
            + $"Cursor row {_row + 1}, column {Math.Min(_column + 1, Width)}\n"
            + "Text at/above cursor:\n" + CursorContext()
            + "\nVisible screen:\n" + Snapshot();
    }
}
