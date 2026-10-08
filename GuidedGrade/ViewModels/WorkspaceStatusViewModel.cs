using UI_Framework;
using System.Windows.Controls;
namespace GuidedGrade.ViewModels;
internal sealed class WorkspaceStatusViewModel(State<bool> showQueue, State<string> queueSummary, State<int> buildCount, State<bool> logLoaded, State<string> violationCount, State<string> violationColor, Action queue, Action violations, Func<View> grade)
{
 internal State<bool> ShowQueue { get; } = showQueue;
 internal State<string> QueueSummary { get; } = queueSummary;
 internal State<int> BuildCount { get; } = buildCount;
 internal State<bool> LogLoaded { get; } = logLoaded;
 internal State<string> ViolationCount { get; } = violationCount;
 internal State<string> ViolationColor { get; } = violationColor;
 internal Action Queue { get; } = queue;
 internal Action Violations { get; } = violations;
 internal Func<View> Grade { get; } = grade;
}
