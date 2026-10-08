using UI_Framework;
using System.Windows.Controls;
namespace GuidedGrade.ViewModels;
internal sealed class ToolsPanelToolbarViewModel(State<bool> toolsVisible, State<bool> violationsSelected, State<bool> logSelected, Action logs, Func<Menu> logOptions, State<string> violationCount, Func<UI_Framework.View> status, Action console, Action violations, Action toggle)
{
 internal State<string> ViolationCount { get; } = violationCount;
 internal Func<UI_Framework.View> Status { get; } = status;
 internal State<bool> ToolsVisible { get; } = toolsVisible;
 internal State<bool> ViolationsSelected { get; } = violationsSelected;
 internal State<bool> LogSelected { get; } = logSelected;
 internal Action Logs { get; } = logs;
 internal Func<Menu> LogOptions { get; } = logOptions;
 internal Action Console { get; } = console;
 internal Action Violations { get; } = violations;
 internal Action Toggle { get; } = toggle;
}
