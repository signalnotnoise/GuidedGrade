using System.Globalization;
using System.Text.RegularExpressions;
using GuidedGrade.Models;
namespace GuidedGrade.Services;
internal static class AssignmentPromptImporter
{
    internal static GradingAssignment Parse(string text)
    {
        var result = new GradingAssignment();
        var requirements = new List<string>();
        var output = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().Trim('"').Replace("**", "");
            if (line.StartsWith("Provide your final output", StringComparison.OrdinalIgnoreCase)
                || Regex.IsMatch(line, @"^(?:#{1,6}\s*)?(?:output|feedback|response)\s+format\s*:?.*$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100))) output = true;
            if (output) continue;
            var match = Regex.Match(line, @"^\s*[*\-]?\s*(?<points>-?\d+(?:\.\d+)?)\s*(?:pts?|points?)\s*(?:deduction)?\s*:\s*(?<rule>.+)$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
            if (!match.Success)
                match = Regex.Match(line, @"^\s*[*\-]?\s*(?<rule>[^:#].*?)\s*:\s*(?:\d+(?:\.\d+)?\s*%\s*/\s*)?(?<points>-?\d+(?:\.\d+)?)\s*(?:pts?|points?)?\s*$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
            if (match.Success)
            {
                var points = double.Parse(match.Groups["points"].Value, CultureInfo.InvariantCulture);
                if (!double.IsFinite(points) || points == 0) throw new ArgumentException("Imported points must be finite and nonzero.");
                var rule = match.Groups["rule"].Value.Trim();
                if (points > 0) result.Rubric.Add(new RubricItem(rule, points));
                else result.Deductions.Add(new AssignmentDeduction { Rule = rule, Points = -points,
                    RequiresInstructorConfirmation = Regex.IsMatch(rule, @"external resources|course policy|removing existing", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)) });
            }
            else if (Regex.IsMatch(line, @"^\s*[*\-]?\s*-?\d+(?:\.\d+)?\s*(?:pts?|points?)\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                throw new ArgumentException("Could not read this points row: " + line);
            else if (line.Length > 0 && !line.StartsWith("Act as", StringComparison.OrdinalIgnoreCase)
                && !Regex.IsMatch(line, @"^You are an? (?:expert )?.*instructor", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100))
                && !line.StartsWith("CRITICAL INSTRUCTION", StringComparison.OrdinalIgnoreCase)
                && !line.StartsWith("Evaluate the provided", StringComparison.OrdinalIgnoreCase)
                && !line.StartsWith("Apply the following", StringComparison.OrdinalIgnoreCase)) requirements.Add(line);
        }
        if (result.Rubric.Count == 0) throw new ArgumentException("No rubric rows found. Use a format such as **10pts:** Item class created or **Item class:** 10.");
        result.Requirements = string.Join("\n", requirements);
        return result;
    }
}
