using System.Globalization;
namespace GuidedGrade.Services;
public sealed record WindowInputAction(string Kind, ushort VirtualKey = 0, double X = 0, double Y = 0, bool RightButton = false)
{
    internal static bool TryParse(string kind, string value, out WindowInputAction? action)
    {
        action = null;
        if (kind == "key")
        {
            var key = value.Trim().ToUpperInvariant();
            key = key switch { "SPACEBAR" => "SPACE", "ESCAPE" => "ESC", "RETURN" => "ENTER",
                "ARROWUP" or "ARROW UP" => "UP", "ARROWDOWN" or "ARROW DOWN" => "DOWN",
                "ARROWLEFT" or "ARROW LEFT" => "LEFT", "ARROWRIGHT" or "ARROW RIGHT" => "RIGHT", _ => key };
            ushort code = key switch { "SPACE" => 0x20, "ESC" => 0x1B, "ENTER" => 0x0D, "TAB" => 0x09,
                "LEFT" => 0x25, "UP" => 0x26, "RIGHT" => 0x27, "DOWN" => 0x28, _ => 0 };
            if (key.Length == 1 && (key[0] is >= 'A' and <= 'Z' or >= '0' and <= '9')) code = key[0];
            if (code == 0) return false;
            action = new(kind, code); return true;
        }
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (kind != "click" || parts.Length != 3 || parts[0] is not ("left" or "right") ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !double.IsFinite(x) || !double.IsFinite(y) || x < 0 || x > 1 || y < 0 || y > 1) return false;
        action = new(kind, X: x, Y: y, RightButton: parts[0] == "right"); return true;
    }
}
