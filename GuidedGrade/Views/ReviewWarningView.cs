using GuidedGrade.ViewModels;
using UI_Framework;
namespace GuidedGrade.Views;
internal sealed class ReviewWarningView(ReviewWarningViewModel model)
{
    internal View Build() => new ToolbarActionView(new("⚠", model.Show, "Review validation warnings", Color: "#FFD166", Hint: string.Join("\n", model.Details))).Build();
}
