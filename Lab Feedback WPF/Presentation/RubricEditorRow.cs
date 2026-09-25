using System.Globalization;
using UI_Framework;

namespace Lab_Feedback_WPF.Presentation;

internal sealed class RubricEditorRow(string name, double points)
{
    internal string Id { get; } = Guid.NewGuid().ToString("N");
    internal State<string> Name { get; } = new(name);
    internal State<string> Points { get; } = new(points.ToString(CultureInfo.InvariantCulture));
    internal bool TryPoints(out double points) => double.TryParse(Points.Value, NumberStyles.Float,
        CultureInfo.InvariantCulture, out points) && double.IsFinite(points) && points > 0;
}
