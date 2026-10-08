using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class PanelHeaderViewModel(State<int> activePanel, Action close)
{
 internal string Title => activePanel.Value switch { 0 => "Feedback", 1 => "Assignment rubric", 2 => "Job queue", _ => "Review panel" };
 internal Action Close { get; } = close;
}
