using UI_Framework;
using GuidedGrade.ViewModels;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class ReviewRubricView(ReviewRubricViewModel model)
{
 internal View Build() => Scroll(VStack(
  Text(model.Assignment?.Title ?? "Choose an assignment").FontSize(21),
  Text($"{model.Assignment?.TotalMaxPoints ?? 0:0.##} points total").FontSize(13),
  VStack(model.Assignment?.Rubric.Select((item, index) => FlexRow(Text(item.Name).Flex(1), Text($"{item.MaxPoints:0.##} pts").Flex(0)).Spacing(12).Padding(8).Id(index.ToString())).ToArray() ?? []).Spacing(4),
  Text("Deductions").FontSize(16),
  VStack(model.Assignment?.Deductions.Select((item,index) => Text($"âˆ’{item.Points:0.##} Â· {item.Rule}" + (item.RequiresInstructorConfirmation ? " (instructor confirmation)" : "")).Id("deduction-" + index)).ToArray() ?? []).Spacing(8),
  Toggle("Show assignment instructions", model.ShowInstructions),
  model.ShowInstructions.Value ? Text(model.Assignment?.Requirements ?? "Set up the assignment to add instructions.") : Text(""),
  Button("Edit assignment", model.Edit)
 ).Spacing(12).Padding(16));
}
