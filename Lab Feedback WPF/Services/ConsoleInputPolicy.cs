using System.Text.RegularExpressions;

namespace Lab_Feedback_WPF.Services;

internal static class ConsoleInputPolicy
{
    private static readonly Regex MenuOption = new(
        @"^\s*(?:\[(?<key>\d+|[A-Za-z])\]|(?<key>\d+|[A-Za-z])[).])\s*(?<label>\S.*)\s*$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static bool TryResolve(string input, string cursorContext, out string resolved, out string reason, string source = "")
    {
        reason = string.Empty;
        if (!ConsoleDriverAgent.TryNormalizeInput(input, out resolved))
        {
            reason = "Model input must be one line without control characters.";
            return false;
        }
        if (ExpectsInteger(cursorContext, source))
        {
            if (Regex.IsMatch(resolved.Trim(), @"^[+-]?\d+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return true;
            reason = "The current prompt reads one integer per line. Send one number, not a list, label or sentence.";
            return false;
        }
        var options = cursorContext.Split('\n').Select(line => (Line: line.Trim(), Match: MenuOption.Match(line)))
            .Where(option => option.Match.Success).ToArray();
        // A single numbered sentence is not enough evidence of a choice menu.
        if (options.Length < 2) return true;
        var candidate = resolved.Trim();
        var matches = options.Where(option =>
            candidate.Equals(option.Match.Groups["key"].Value, StringComparison.OrdinalIgnoreCase)
            || candidate.Equals(option.Line, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1)
        {
            reason = "Model input did not identify exactly one currently displayed menu option. No input was sent.";
            return false;
        }
        resolved = matches[0].Match.Groups["key"].Value;
        return true;
    }

    // Conservative evidence: exact visible prompt literal, followed by getline(cin, variable)
    // and stoi(variable) before another output statement. Do not infer from the word 'score' alone.
    internal static bool ExpectsInteger(string cursorContext, string source)
    {
        if (string.IsNullOrEmpty(source)) return false;
        foreach (var line in cursorContext.Split('\n').Reverse().Select(l => l.Trim()).Where(l => l.Length >= 8))
        {
            var literal = source.IndexOf('"' + line + '"', StringComparison.Ordinal);
            if (literal < 0) continue;
            var start = literal + line.Length + 2;
            var next = source.Substring(start, Math.Min(2000, source.Length - start));
            var nextPrint = next.IndexOf("cout", StringComparison.Ordinal);
            if (nextPrint >= 0) next = next[..nextPrint];
            var read = Regex.Match(next, @"getline\s*\(\s*(?:std::)?cin\s*,\s*(?<name>\w+)\s*\)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (!read.Success) return false;
            return Regex.IsMatch(next[(read.Index + read.Length)..], @"\bstoi\s*\(\s*" + Regex.Escape(read.Groups["name"].Value) + @"\s*\)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        return false;
    }
}
