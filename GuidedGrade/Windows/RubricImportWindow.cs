using System.Globalization;
using System.Windows;
using GuidedGrade.Models;
using GuidedGrade.Presentation;
using UI_Framework;
using static UI_Framework.UI;

namespace GuidedGrade.Windows;

internal sealed class RubricImportWindow : ReviewWindow
{
    private readonly State<string> _text = new("");
    internal List<RubricItem> Items { get; } = new();
    internal RubricImportWindow()
    {
        Title = "Import rubric"; Width = 620; Height = 560; MinWidth = 400; MinHeight = 380;
        ShowView(() => Scroll(VStack(Text("Paste rubric").FontSize(22),
            Text("One criterion per line, with points at the end. Example: Input validation 2.5"),
            TextEditor(_text).Height(320).AccessibilityLabel("Rubric text"),
            HStack(Button("Import", Import).ButtonStyle(ButtonStyleKind.Primary), Button("Cancel", () => DialogResult = false)).Spacing(8)
        ).Spacing(12).Padding(20)));
    }
    private void Import()
    {
        var parsed = new List<RubricItem>();
        foreach (var line in _text.Value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2 || !double.TryParse(tokens[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var points)
                || !double.IsFinite(points) || points <= 0)
            { MessageBox.Show(this, "Every line needs a criterion followed by positive points. Check: " + line, "Import rubric"); return; }
            parsed.Add(new RubricItem(string.Join(" ", tokens[..^1]), points));
        }
        if (parsed.Count == 0) { MessageBox.Show(this, "Paste at least one rubric criterion.", "Import rubric"); return; }
        Items.AddRange(parsed);
        DialogResult = true;
    }
}
