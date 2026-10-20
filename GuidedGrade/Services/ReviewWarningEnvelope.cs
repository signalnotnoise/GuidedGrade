using System.Text;
using System.Text.Json;
using GuidedGrade.Models;
namespace GuidedGrade.Services;
internal static class ReviewWarningEnvelope
{
    private const string Marker = "\n<!--guidedgrade-review-warnings:";
    internal static string Pack(string text, IEnumerable<string> warnings)
    {
        var list = warnings.Distinct().ToArray();
        return list.Length == 0 ? text : text + Marker + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(list))) + "-->";
    }
    internal static (string Text, List<string> Warnings) Unpack(string text)
    {
        var start = text.LastIndexOf(Marker, StringComparison.Ordinal);
        if (start < 0 || !text.EndsWith("-->", StringComparison.Ordinal)) return (text, []);
        try { return (text[..start], JsonSerializer.Deserialize<List<string>>(Encoding.UTF8.GetString(Convert.FromBase64String(text[(start+Marker.Length)..^3]))) ?? []); }
        catch (Exception ex) when (ex is FormatException or JsonException) { return (text, []); }
    }
    internal static string Draft(string reply, GradingAssignment assignment, IReadOnlyList<OllamaService.CodeFile> files, string failure)
    {
        var warnings = new List<string> { failure };
        var sb = new StringBuilder();
        try
        {
            var response = JsonSerializer.Deserialize<OverallReviewResult.Response>(reply, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var rows = response?.Criteria ?? [];
            sb.AppendLine("Score Breakdown:");
            foreach (var criterion in assignment.Rubric.Select((r,i) => (Rule:r, Id:i+1)))
            {
                var matching = rows.Where(r => r != null && r.Id == criterion.Id).ToArray();
                if (matching.Length != 1)
                {
                    warnings.Add($"Criterion {criterion.Id} ({criterion.Rule.Name}): {(matching.Length == 0 ? "missing" : "duplicated")} model row.");
                    sb.AppendLine($"- {criterion.Rule.Name}: unverified"); continue;
                }
                var row = matching[0];
                sb.AppendLine($"- {criterion.Rule.Name}: {(row.Earned.HasValue ? row.Earned.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "unverified")}/{criterion.Rule.MaxPoints} (unverified) — {row.Reason}");
                if (row.Earned > criterion.Rule.MaxPoints || row.Earned < 0) warnings.Add($"Criterion {row.Id} maps to {criterion.Rule.Name} (maximum {criterion.Rule.MaxPoints}), but the model awarded {row.Earned}. IDs may be shifted or points are out of range.");
                if (row.File < 1 || row.File > files.Count || string.IsNullOrWhiteSpace(row.Quote) || !files[row.File-1].Content.Contains(row.Quote, StringComparison.Ordinal)) warnings.Add($"Criterion {row.Id} ({criterion.Rule.Name}): quoted evidence does not match file-{row.File}. Check rewritten whitespace, placeholder code, or incorrect file number.");
            }
            foreach (var row in rows.Where(r => r != null && (r.Id < 1 || r.Id > assignment.Rubric.Count))) warnings.Add($"Unknown criterion ID {row.Id}.");
            foreach (var deduction in response?.Deductions ?? [])
                if (deduction != null) warnings.Add($"Deduction ID {deduction.Id}: not applied in this invalid draft; verify configured rule and source evidence. Model reason: {deduction.Reason}");
            sb.AppendLine("\nDeductions Applied: Unverified — review required.");
            sb.AppendLine("\nFinal Grade: Withheld pending instructor review.");
            if (!string.IsNullOrWhiteSpace(response?.Feedback)) sb.AppendLine("\nFeedback: " + response.Feedback);
        }
        catch (JsonException)
        {
            warnings.Add("The response was not valid JSON; displayed as editable prose instead.");
            sb.AppendLine("Feedback:\n" + reply);
        }
        return Pack(sb.ToString().Trim(), warnings);
    }
}
