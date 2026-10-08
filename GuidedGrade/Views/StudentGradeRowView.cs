using UI_Framework;
using GuidedGrade.ViewModels;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class StudentGradeRowView(StudentGradeRowViewModel model)
{
 internal View Build() => FlexRow(Text(model.Name).FontSize(13).Flex(1),
  VStack(new GradeBadgeView(new GradeBadgeViewModel(model.Assignment, "Assignment")).Build(),
   new GradeBadgeView(new GradeBadgeViewModel(model.Total, "Total")).Build()).Spacing(3).Flex(0)).Spacing(8).Padding(4).Background("#1B1E23");
}
