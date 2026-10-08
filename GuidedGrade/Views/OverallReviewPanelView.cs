using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class OverallReviewPanelView(OverallReviewPanelViewModel model)
{
    internal View Build() => VStack(model.Pending.Select(review => VStack(Text("Overall review · awaiting approval"),
        Text(review.Explanation), Button("Approve overall feedback", () => model.Approve(review)))).ToArray()).Spacing(8);
}
