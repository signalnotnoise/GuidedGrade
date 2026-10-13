using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class UnplacedReviewsView(UnplacedReviewsViewModel model)
{
    internal View Build() => model.Comments.Count == 0 ? Text("") : VStack(
        Text("Unplaced comments").FontSize(16),
        Text("No reliable source location was found. Choose a file and line before approving."),
        VStack(model.Comments.Select(row => VStack(Text(row.Comment.SectionName), Text(row.Comment.Explanation),
            Button("Choose file and line", () => model.Place(row.Path, row.Comment))
                .AccessibilityLabel("Place review: " + row.Comment.SectionName)).Spacing(6)).ToArray()).Spacing(12)).Spacing(8);
}
