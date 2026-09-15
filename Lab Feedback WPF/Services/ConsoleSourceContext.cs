using System.Text;
using System.Text.RegularExpressions;

namespace Lab_Feedback_WPF.Services;

internal static class ConsoleSourceContext
{
    internal static string Build(string source)
    {
        var lines = source.Split('\n');
        var selected = new SortedSet<int>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (selected.Count >= 1024) break;
            var line = lines[i].TrimStart();
            if (line.StartsWith("//") || line.StartsWith('*')) continue;
            if (!Regex.IsMatch(line, @"\b(getline|stoi|scanf|ReadLine|readLine|input)\s*\(|\bcin\s*>>", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) continue;
            for (var j = Math.Max(0, i - 8); j <= Math.Min(lines.Length - 1, i + 8); j++) selected.Add(j);
        }
        if (selected.Count == 0) return source.Length <= 4000 ? source : source[..4000];
        var result = new StringBuilder();
        var previous = -2;
        foreach (var index in selected)
        {
            if (result.Length >= 10000) break;
            if (index != previous + 1) result.AppendLine("[input-reading excerpt]");
            if (result.Length >= 10000) break;
            var line = lines[index];
            result.AppendLine(line[..Math.Min(line.Length, 10000 - result.Length)]);
            previous = index;
        }
        return result.ToString();
    }
}
