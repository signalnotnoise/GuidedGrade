using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class WorkspaceToolbarView(WorkspaceToolbarViewModel model)
{
 internal View Build() => FlexRow(
  WpfUI.Native(model.Menu).Id("application-menu").Flex(0),
  Button("Open submissions", model.Open).FontSize(12).Flex(0),
  model.ReviewMenu().Flex(0),
  new MenuSelectorView(new("Course", model.Courses.ToArray(), model.CourseIndex.Value, model.SelectCourse, "Saved course")).Build().Flex(0),
  new MenuSelectorView(new("Assignment", model.Assignments.ToArray(), model.AssignmentIndex.Value, model.SelectAssignment, "Saved assignment")).Build().Flex(0),
  model.PanelTabs().Flex(0)
 ).Spacing(8).Padding(6).Background(ReviewTheme.Tokens.Surface);
}
