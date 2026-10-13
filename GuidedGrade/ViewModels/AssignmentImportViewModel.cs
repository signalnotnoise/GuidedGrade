using GuidedGrade.Models;
using GuidedGrade.Services;
using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class AssignmentImportViewModel
{
    internal State<string> Text { get; } = new("");
    internal State<string> Preview { get; } = new("");
    internal State<string> Error { get; } = new("");
    private string _previewed = "";
    internal GradingAssignment? Parsed { get; private set; }
    internal bool CanApply => Parsed != null && _previewed == Text.Value;
    internal void Parse()
    {
        Parsed = null; Error.Value = "";
        try
        {
            Parsed = AssignmentPromptImporter.Parse(Text.Value); _previewed = Text.Value;
            Preview.Value = $"Rubric ({Parsed.TotalMaxPoints:0.##} points):\n" + string.Join("\n", Parsed.Rubric.Select(r => $"{r.MaxPoints:0.##}: {r.Name}"))
                + "\n\nDeductions:\n" + string.Join("\n", Parsed.Deductions.Select(d => $"-{d.Points:0.##}: {d.Rule}" + (d.RequiresInstructorConfirmation ? " [instructor confirmation]" : "")))
                + "\n\nInstructions:\n" + Parsed.Requirements + "\n\nFeedback: you/your; score breakdown, deductions, final grade and feedback. Review imported wording after applying. Applying replaces the current instructions, rubric, deductions and feedback preferences.";
        }
        catch (Exception ex) { Preview.Value = ""; Error.Value = ex.Message; }
    }
}
