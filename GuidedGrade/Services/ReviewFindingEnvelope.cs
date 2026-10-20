using System.Text;
using System.Text.Json;
using GuidedGrade.Models;
namespace GuidedGrade.Services;
internal static class ReviewFindingEnvelope
{
    private const string Marker = "\n<!--guidedgrade-rubric-findings:";
    internal sealed record FindingDraft(int Id, string Name, double Maximum, double? Earned, bool Verified, int File, string Quote, string Reason, bool IsDeduction = false, bool RequiresInstructorConfirmation = false);
    internal static string Pack(string text, string reply, GradingAssignment assignment, IReadOnlyList<OllamaService.CodeFile> files)
    {
        OverallReviewResult.Response? response;
        try { response = JsonSerializer.Deserialize<OverallReviewResult.Response>(reply, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return text; }
        var rows = response?.Criteria ?? [];
        var drafts = assignment.Rubric.Select((criterion, index) =>
        {
            var matches = rows.Where(row => row != null && row.Id == index + 1).ToArray();
            var row = matches.Length == 1 ? matches[0] : null;
            var matchFile = row == null ? 0 : OverallReviewResult.FindEvidenceFile(files, row);
            var validPoints = row?.Earned is double points && double.IsFinite(points) && points >= 0 && points <= criterion.MaxPoints;
            return new FindingDraft(index + 1, criterion.Name, criterion.MaxPoints, validPoints ? row!.Earned : null,
                row?.Status == "verified" && validPoints && matchFile > 0, matchFile > 0 ? matchFile : row?.File ?? 0,
                row?.Quote ?? "", row?.Reason ?? "The model did not provide a unique finding. Review this criterion yourself.");
        }).ToList();
        foreach (var row in (response?.Deductions ?? []).Where(row => row != null && row.Id >= 1 && row.Id <= assignment.Deductions.Count).GroupBy(row => row.Id).Where(group => group.Count() == 1).Select(group => group.Single()))
        {
            var rule = assignment.Deductions[row.Id - 1];
            var matchFile = OverallReviewResult.FindEvidenceFile(files, row);
            drafts.Add(new(row.Id, "Deduction: " + rule.Rule, rule.Points, rule.Points,
                row.Status == "verified" && matchFile > 0, matchFile > 0 ? matchFile : row.File,
                row.Quote, row.Reason, true, rule.RequiresInstructorConfirmation));
        }
        if (!string.IsNullOrWhiteSpace(response?.Feedback))
            drafts.Add(new(0, "Overall feedback", 0, null, false, 1, "", response.Feedback));
        return text + Marker + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(drafts))) + "-->";
    }
    internal static (string Text, List<FindingDraft> Findings) Unpack(string text)
    {
        var start = text.LastIndexOf(Marker, StringComparison.Ordinal);
        if (start < 0 || !text.EndsWith("-->", StringComparison.Ordinal)) return (text, []);
        return (text[..start], JsonSerializer.Deserialize<List<FindingDraft>>(Encoding.UTF8.GetString(Convert.FromBase64String(text[(start + Marker.Length)..^3]))) ?? []);
    }
}
