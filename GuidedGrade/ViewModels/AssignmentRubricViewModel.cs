using GuidedGrade.Models;
using GuidedGrade.Presentation;
using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class AssignmentRubricViewModel
{
    internal StateList<RubricEditorRow> Rows { get; } = new();
    internal string TotalLabel => "Total: " + (Rows.All(r => r.TryPoints(out _)) ? Rows.Sum(r => { r.TryPoints(out var p); return p; }).ToString("0.##") + " points" : "check point values");
    internal void Load(IEnumerable<RubricItem> items) { Rows.Clear(); foreach (var item in items) Rows.Add(new(item.Name, item.MaxPoints)); }
    internal List<RubricItem> Build() => Rows.Select(row => row.TryPoints(out var points) && !string.IsNullOrWhiteSpace(row.Name.Value)
        ? new RubricItem(row.Name.Value.Trim(), points) : throw new ArgumentException("Every rubric row needs a name and positive points.")).ToList();
}
