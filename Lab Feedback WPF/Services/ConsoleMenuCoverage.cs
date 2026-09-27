using System.Text.RegularExpressions;

namespace Lab_Feedback_WPF.Services;

// Tracks selections, not successful assertions about student behavior.
internal sealed class ConsoleMenuCoverage
{
    private readonly Dictionary<string, HashSet<string>> selected = new();
    private readonly Dictionary<string, string[]> labels = new();
    private string? current;
    private string? key;
    private static readonly Regex Option = new(@"^\s*(?:\[(?<key>\d+|[A-Za-z])\]|(?<key>\d+|[A-Za-z])[).])\s*(?<label>\S.*?)\s*$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public ConsoleAgentAction? Choose(string context)
    {
        current = key = null;
        var lines = context.Split('\n');
        var options = lines.Select((line, index) => (Match: Option.Match(line), Index: index)).Where(x => x.Match.Success).ToArray();
        if (options.Length < 2) return null;
        // Do not interpret a previous menu above a new item/name/quantity question as active.
        var trailing = string.Join(" ", lines.Skip(options[^1].Index + 1)).Trim();
        if (trailing.Length > 0 && !Regex.IsMatch(trailing, @"^(?:[_>:?\s]+|(?:(?:please\s+)?(?:enter|select|choose)\s+)?(?:your\s+)?(?:option\s+choice|option|choice|selection)\s*[:?>_ ]*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return null;
        var entries = options.Select(x => (Key: x.Match.Groups["key"].Value, Label: x.Match.Groups["label"].Value)).ToArray();
        if (entries.Select(x => x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length) return null;
        var heading = lines.Take(options[0].Index).LastOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim() ?? "(untitled menu)";
        var id = heading + "|" + string.Join("|", entries.Select(x => x.Key + ":" + x.Label));
        if (!selected.TryGetValue(id, out var visited))
        {
            if (selected.Count >= 16) return null;
            selected[id] = visited = new HashSet<string>();
            labels[id] = entries.Select(x => x.Key + ": " + x.Label).ToArray();
        }
        var ordered = entries.OrderBy(x => Regex.IsMatch(x.Label, @"^(?:leave|exit|quit|back|return)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));
        var next = ordered.FirstOrDefault(x => !visited.Contains(x.Key));
        if (next.Key == null) return new ConsoleAgentAction { Action = "stop", Reason = "All recognized options on this menu have already been selected; stopping repeated traversal." };
        current = id;
        key = next.Key;
        return new ConsoleAgentAction { Action = "type", Input = next.Key, Reason = "Menu coverage: select " + next.Label + "; exit options are scheduled last." };
    }

    public void RecordSent()
    {
        if (current != null && key != null) selected[current].Add(key);
    }

    public IEnumerable<string> Summary() => labels.SelectMany(menu => menu.Value.Select(label =>
        "[menu coverage] " + (selected[menu.Key].Contains(label.Split(':')[0]) ? "Selected: " : "UNTESTED: ") + label));
}
