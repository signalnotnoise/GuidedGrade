using GuidedGrade.ViewModels;
using GuidedGrade.Presentation;
using UI_Framework;
namespace GuidedGrade.Views;
internal sealed class PanelToggleView(PanelToggleViewModel model)
{
    internal View Build() => new ToolbarActionView(new((model.Left ? "left" : "right") + model.Expanded, model.Toggle,
        model.Left ? "Toggle submissions panel" : "Toggle review panel", Color: ReviewTheme.Tokens.Ink,
        Hint: (model.Expanded ? "Hide " : "Show ") + (model.Left ? "submissions panel" : "review panel")),
        () => new PanelIconView(new(model.Left, model.Expanded)).Build()).Build();
}
