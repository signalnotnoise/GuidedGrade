using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class ToolsPanelToolbarView(ToolsPanelToolbarViewModel model)
{
 internal View Build() => FlexRow(
    new NavigationTabView(new("Console", model.ToolsVisible.Value && !model.LogSelected.Value && !model.ViolationsSelected.Value, model.Console, "Console tab")).Build().Flex(0),
    new NavigationTabView(new("⚠ " + model.ViolationCount.Value, model.ToolsVisible.Value && !model.LogSelected.Value && model.ViolationsSelected.Value, model.Violations, "Violations tab")).Build().Flex(0),
    new NavigationTabView(new("▤ Logs", model.ToolsVisible.Value && model.LogSelected.Value, model.Logs, "Logs tab")).Build().Flex(0),
    WpfUI.Native(model.LogOptions).Id("log-options").Flex(0),
    model.Status().Flex(1),
    new ToolbarActionView(new(model.ToolsVisible.Value ? "⌄" : "⌃", model.Toggle, "Toggle tools panel", Hint: model.ToolsVisible.Value ? "Hide bottom panel" : "Show bottom panel")).Build().Flex(0)
 ).Spacing(4).Padding(4).Background(ReviewTheme.Tokens.Surface);
}
