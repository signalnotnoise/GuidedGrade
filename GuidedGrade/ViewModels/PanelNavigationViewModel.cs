using UI_Framework;
using System.Windows.Controls;
namespace GuidedGrade.ViewModels;
internal sealed class PanelNavigationViewModel(State<bool> showComments, State<bool> showQueue, State<int> activePanel, Action feedback, Action queue, Action rubric, Action close)
{
 internal State<bool> ShowComments { get; } = showComments;
 internal State<bool> ShowQueue { get; } = showQueue;
 internal State<int> ActivePanel { get; } = activePanel;
 internal Action Feedback { get; } = feedback;
 internal Action Queue { get; } = queue;
 internal Action Rubric { get; } = rubric;
 internal Action Close { get; } = close;
}
