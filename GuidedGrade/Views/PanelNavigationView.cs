using UI_Framework;
using GuidedGrade.ViewModels;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class PanelNavigationView(PanelNavigationViewModel model)
{
 internal View Build()
 {
  var tabs = new List<View>();
  if (model.ShowComments.Value) tabs.Add(new NavigationTabView(new("Feedback", model.ActivePanel.Value == 0, model.Feedback, "Comments panel")).Build());
  if (model.ShowQueue.Value) tabs.Add(new NavigationTabView(new("Queue", model.ActivePanel.Value == 2, model.Queue, "Job queue panel")).Build());
  tabs.Add(new NavigationTabView(new("Rubric", model.ActivePanel.Value == 1, model.Rubric, "Rubric panel")).Build());
  return HStack(tabs.ToArray()).Spacing(0);
 }
}
