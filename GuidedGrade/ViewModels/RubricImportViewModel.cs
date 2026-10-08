using System.Globalization;
using GuidedGrade.Models;
using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class RubricImportViewModel
{
    internal State<string> Text { get; } = new("");
    internal State<string> Error { get; } = new("");
    internal List<RubricItem> Parse()
    {
        var result = new List<RubricItem>();
        foreach (var line in Text.Value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2 || !double.TryParse(tokens[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var points) || !double.IsFinite(points) || points <= 0)
                throw new ArgumentException("Every line needs a criterion followed by positive points. Check: " + line);
            result.Add(new RubricItem(string.Join(" ", tokens[..^1]), points));
        }
        if (result.Count == 0) throw new ArgumentException("Paste at least one rubric criterion.");
        return result;
    }
}
