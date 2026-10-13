using GuidedGrade.Services;
using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class EarlierReviewsView(EarlierReviewsViewModel model)
{
 internal View Build() => model.Comments.Length == 0 ? Text("") : VStack(
  Text("Earlier reviews · assignment not recorded").FontSize(14),
  Text("These saved comments belong to this file. They are not included in this assignment's feedback or grade.").FontSize(12),
  VStack(model.Comments.Select((comment, index) => Text(SavedFeedbackText.Format(comment)).Id("earlier-" + index)).ToArray()).Spacing(10)
 ).Spacing(8).Padding(8);
}
