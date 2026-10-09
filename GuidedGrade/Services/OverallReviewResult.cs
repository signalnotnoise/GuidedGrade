using System.Globalization;
using System.Text;
using System.Text.Json;
using GuidedGrade.Models;
namespace GuidedGrade.Services;
internal static class OverallReviewResult
{
    internal sealed class Finding
    {
        public int Id { get; set; }
        public double? Earned { get; set; }
        public string Status { get; set; } = "";
        public int File { get; set; }
        public string Quote { get; set; } = "";
        public string Reason { get; set; } = "";
    }
    internal sealed class Response
    {
        public List<Finding> Criteria { get; set; } = [];
        public List<Finding> Deductions { get; set; } = [];
        public string Feedback { get; set; } = "";
    }
    internal static readonly JsonElement Schema = JsonSerializer.SerializeToElement(new
    {
        type = "object", additionalProperties = false,
        properties = new
        {
            criteria = new { type = "array", items = ItemSchema },
            deductions = new { type = "array", items = ItemSchema },
            feedback = new { type = "string" }
        }, required = new[] { "criteria", "deductions", "feedback" }
    });
    private static object ItemSchema => new
    {
        type = "object", additionalProperties = false,
        properties = new Dictionary<string, object>
        {
            ["id"] = new { type = "integer" },
            ["earned"] = new { type = new[] { "number", "null" } },
            ["status"] = new { type = "string", @enum = new[] { "verified", "unverified" } },
            ["file"] = new { type = "integer" },
            ["quote"] = new { type = "string" },
            ["reason"] = new { type = "string" }
        }, required = new[] { "id", "earned", "status", "file", "quote", "reason" }
    };
    internal static JsonElement SchemaFor(GradingAssignment assignment)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(Schema.GetRawText())!;
        var criteria = node["properties"]!["criteria"]!;
        criteria["minItems"] = assignment.Rubric.Count;
        criteria["maxItems"] = assignment.Rubric.Count;
        criteria["items"]!["properties"]!["id"]!["enum"] = System.Text.Json.JsonSerializer.SerializeToNode(Enumerable.Range(1, assignment.Rubric.Count).ToArray());
        return JsonSerializer.SerializeToElement(node);
    }
    internal static string Contract(GradingAssignment assignment)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# MACHINE REVIEW CONTRACT (overrides prose output formatting only)");
        sb.AppendLine("Return only JSON matching this schema: " + SchemaFor(assignment).GetRawText());
        sb.AppendLine("Return every criterion exactly once using its ID below. Evaluate all supplied files together. earned is the criterion's awarded points, never a total. status is verified only when supported by supplied code. Supply a short exact contiguous source quote and 1-based file number for every verified criterion or deduction. For unverified or missing context use earned:null, file:0, quote:empty and explain the uncertainty. Never interpret a declaration as an absent definition without inspecting every file. For alleged missing code, quote the incomplete body or relevant supplied code and explain the search; if the implementation file is absent, mark unverified. Quotes must preserve punctuation and whitespace. Do not invent build/run evidence.");
        sb.AppendLine("C# calculates totals. Do not return or state overall scores in feedback. Use feedback only for a single student-facing paragraph; no headings, rubric list or repeated review. deductions contains only separately configured penalty IDs that have source evidence; do not include rubric losses. Use earned:null for deductions; C# uses configured penalty amounts. Do not invent deductions. Instructor-confirmation deductions are flags, not applied penalties.");
        for (var i = 0; i < assignment.Rubric.Count; i++) sb.AppendLine($"Criterion ID {i+1}: rubric row {i+1} in the sanitized assignment rubric above | maximum {assignment.Rubric[i].MaxPoints.ToString(CultureInfo.InvariantCulture)}");
        for (var i = 0; i < assignment.Deductions.Count; i++) sb.AppendLine($"Deduction ID {i+1}: configured deduction row {i+1} above");
        return sb.ToString();
    }
    internal static string Render(string json, GradingAssignment assignment, IReadOnlyList<OllamaService.CodeFile> files, bool allowIncomplete = false)
    {
        Response result;
        try { result = JsonSerializer.Deserialize<Response>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new JsonException(); }
        catch (JsonException) { throw new InvalidOperationException("The model returned an invalid structured review. No grade or feedback was saved."); }
        if (result.Criteria == null || result.Deductions == null || result.Criteria.Any(c => c == null) || result.Deductions.Any(c => c == null))
            throw new InvalidOperationException("The model returned null rubric data. No grade or feedback was saved.");
        var incomplete = result.Criteria.Count != assignment.Rubric.Count || result.Criteria.Select(c => c.Id).Distinct().Count() != assignment.Rubric.Count || result.Criteria.Any(c => c.Id < 1 || c.Id > assignment.Rubric.Count);
        if (incomplete && !allowIncomplete)
        {
            var missing = Enumerable.Range(1, assignment.Rubric.Count).Except(result.Criteria.Select(c => c.Id));
            var duplicates = result.Criteria.GroupBy(c => c.Id).Where(g => g.Count() > 1).Select(g => g.Key);
            throw new InvalidOperationException($"Rubric response mismatch: expected IDs 1–{assignment.Rubric.Count}; received {result.Criteria.Count} rows; missing IDs [{string.Join(",", missing)}]; duplicate IDs [{string.Join(",", duplicates)}].");
        }
        if (incomplete)
        {
            var unique = result.Criteria.GroupBy(c => c.Id).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
            result.Criteria = Enumerable.Range(1, assignment.Rubric.Count).Select(id => unique.GetValueOrDefault(id) ?? new Finding { Id = id, Status = "unverified", Reason = "The model omitted this criterion or supplied conflicting duplicate rows." }).ToList();
            result.Feedback = "This review is incomplete. Some rubric findings could not be obtained reliably; inspect the unverified rows before grading.";
        }
        bool Evidence(Finding f) => f.File >= 1 && f.File <= files.Count && !string.IsNullOrWhiteSpace(f.Quote) && files[f.File-1].Content.Contains(f.Quote, StringComparison.Ordinal);
        var verified = !incomplete; double earned = 0, penalty = 0;
        var sb = new StringBuilder();
        var depth = Math.Clamp(assignment.FeedbackOptions.DetailLevel, 1, 5);
        string Short(string text, int limit) => string.Join(" ", (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(limit));
        var reasonLimit = depth == 1 ? 12 : depth == 2 ? 20 : 80;
        if (assignment.FeedbackOptions.IncludeScoreBreakdown) sb.AppendLine("Score Breakdown:");
        foreach (var f in result.Criteria.OrderBy(c => c.Id))
        {
            var criterion = assignment.Rubric[f.Id-1];
            if (f.Earned.HasValue && (!double.IsFinite(f.Earned.Value) || f.Earned < 0 || f.Earned > criterion.MaxPoints)) throw new InvalidOperationException("The model returned points outside the rubric range. No grade or feedback was saved.");
            var supported = f.Status == "verified" && f.Earned.HasValue && Evidence(f);
            if (!supported) verified = false; else earned += f.Earned!.Value;
            var reason = supported ? Short(f.Reason, reasonLimit) + $" [file-{f.File}]" : "Unverified: " + (Evidence(f) || f.Status != "verified" ? Short(f.Reason, reasonLimit) : "source evidence did not match the supplied files.");
            if (assignment.FeedbackOptions.IncludeScoreBreakdown || !supported)
                sb.AppendLine($"- {criterion.Name}: {(supported ? f.Earned!.Value.ToString("0.##", CultureInfo.InvariantCulture) + "/" + criterion.MaxPoints.ToString("0.##", CultureInfo.InvariantCulture) : "unverified")} — {reason}");
        }
        var deductionLines = new List<string>();
        if (result.Deductions.Select(d => d.Id).Distinct().Count() != result.Deductions.Count) throw new InvalidOperationException("Duplicate deduction IDs in model review.");
        foreach (var f in result.Deductions)
        {
            if (f.Id < 1 || f.Id > assignment.Deductions.Count) throw new InvalidOperationException("The model invented a deduction rule. No grade or feedback was saved.");
            var rule = assignment.Deductions[f.Id-1];
            if (f.Status != "verified" || !Evidence(f)) { verified = false; deductionLines.Add(rule.Rule + ": unverified evidence; not applied."); continue; }
            if (rule.RequiresInstructorConfirmation) deductionLines.Add(rule.Rule + ": requires instructor confirmation; not applied.");
            else { penalty += rule.Points; deductionLines.Add($"-{rule.Points.ToString("0.##", CultureInfo.InvariantCulture)}: {Short(f.Reason, reasonLimit)} [file-{f.File}]"); }
        }
        if (assignment.FeedbackOptions.IncludeDeductions) sb.AppendLine("\nDeductions Applied: " + (deductionLines.Count == 0 ? "None" : "\n" + string.Join("\n", deductionLines.Select(l => "- " + l))));
        if (assignment.FeedbackOptions.IncludeFinalGrade) sb.AppendLine("\nFinal Grade: " + (verified ? $"{Math.Clamp(earned-penalty,0,assignment.TotalMaxPoints).ToString("0.##", CultureInfo.InvariantCulture)}/{assignment.TotalMaxPoints.ToString("0.##", CultureInfo.InvariantCulture)} (suggested)" : "Withheld — one or more findings could not be verified."));
        if (assignment.FeedbackOptions.IncludeFeedback && !string.IsNullOrWhiteSpace(result.Feedback)) sb.AppendLine("\nFeedback: " + Short(result.Feedback, depth == 1 ? 35 : depth == 2 ? 60 : depth == 3 ? 150 : depth == 4 ? 300 : 500));
        return sb.ToString().Trim();
    }
}
