using System.Net;
using System.Text.RegularExpressions;
using System.Text;

namespace Lab_Feedback_WPF.Services;

internal static class FeedbackCopyFormatter
{
    public static string Format(string text, string format)
    {
        if (format == "Markdown") return text;
        if (format == "HTML") return ColorHtml(text);
        var result = new StringBuilder();
        var code = false;
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.TrimStart().StartsWith("```")) { code = !code; continue; }
            if (code) { result.AppendLine(line); continue; }
            var heading = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
            var body = heading.Success ? heading.Groups[2].Value : line;
            result.AppendLine(Regex.Replace(body, @"`([^`]+)`|\*\*(.+?)\*\*", m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value));
        }
        return result.ToString().TrimEnd('\r', '\n');
    }

    private static string ColorHtml(string text)
    {
        var output = new StringBuilder("<article style=\"font-family:Segoe UI,Arial,sans-serif;font-size:15px;line-height:1.6;color:#243247;background:#ffffff;max-width:820px;padding:20px;\">\n");
        var section = false;
        var list = false;
        var code = false;
        void CloseList() { if (list) { output.AppendLine("</ul>"); list = false; } }
        void CloseSection() { CloseList(); if (section) { output.AppendLine("</section>"); section = false; } }
        string Inline(string value)
        {
            var encoded = WebUtility.HtmlEncode(value);
            // Match code first so emphasis inside a code span stays literal.
            return Regex.Replace(encoded, @"`([^`]+)`|\*\*(.+?)\*\*", m => m.Groups[1].Success
                ? "<code style=\"background:#e8edf3;padding:2px 4px;border-radius:3px;\">" + m.Groups[1].Value + "</code>"
                : "<strong>" + m.Groups[2].Value + "</strong>");
        }
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.TrimStart().StartsWith("```"))
            {
                CloseList();
                output.AppendLine(code ? "</code></pre>" : "<pre style=\"background:#f1f5f9;color:#172033;padding:12px;white-space:pre-wrap;border:1px solid #cbd5e1;border-radius:6px;\"><code>");
                code = !code;
                continue;
            }
            if (code) { output.AppendLine(WebUtility.HtmlEncode(line)); continue; }
            var heading = Regex.Match(line.Trim(), @"^(#{1,6})\s+(.+)$");
            var label = (heading.Success ? heading.Groups[2].Value : line.Trim()).Trim('*', ' ', ':');
            var category = label.ToLowerInvariant() switch
            {
                "strengths" or "good" or "what went well" or "correct" => ("#166534", "#f0fdf4", "Strengths"),
                "warnings" or "warning" or "suggestions" or "recommendations" or "inconclusive" => ("#854d0e", "#fffbeb", "Needs attention"),
                "issues" or "errors" or "what went wrong" or "areas to improve" or "failures" => ("#991b1b", "#fef2f2", "Issues to address"),
                _ => ("", "", "")
            };
            if (category.Item1.Length > 0)
            {
                CloseSection();
                section = true;
                output.AppendLine($"<section aria-label=\"{category.Item3}\" style=\"margin:16px 0;padding:14px 18px;border-left:4px solid {category.Item1};background:{category.Item2};border-radius:6px;\">");
                output.AppendLine($"<h3 style=\"margin:0 0 8px;color:{category.Item1};font-size:17px;\">{WebUtility.HtmlEncode(label)}</h3>");
            }
            else if (heading.Success)
            {
                CloseSection();
                var level = heading.Groups[1].Length;
                output.AppendLine($"<h{level}>{Inline(heading.Groups[2].Value)}</h{level}>");
            }
            else if (Regex.IsMatch(line, @"^\s*[-*+]\s+"))
            {
                if (!list) { output.AppendLine("<ul style=\"margin:8px 0;padding-left:22px;\">"); list = true; }
                output.AppendLine("<li style=\"margin:5px 0;\">" + Inline(Regex.Replace(line, @"^\s*[-*+]\s+", "")) + "</li>");
            }
            else
            {
                CloseList();
                if (!string.IsNullOrWhiteSpace(line)) output.AppendLine("<p style=\"margin:8px 0;white-space:pre-wrap;\">" + Inline(line) + "</p>");
            }
        }
        if (code) output.AppendLine("</code></pre>");
        CloseSection();
        return output.Append("</article>").ToString();
    }
}
