using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class FeedbackDialViewModel(State<int> level)
{
    internal State<int> Level { get; } = level;
    internal string Label => new[] { "Very brief", "Brief", "Standard", "Detailed", "Thorough" }[Math.Clamp(Level.Value, 1, 5) - 1];
}
