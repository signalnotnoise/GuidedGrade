using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class WorkspaceStatusView(WorkspaceStatusViewModel model)
{
 internal View Build() {
        var items = new List<View>();
        if (model.ShowQueue.Value) items.Add(new ToolbarActionView(new("≡ " + model.QueueSummary.Value, model.Queue, "Queue status", Hint: "Open job queue")).Build());
        if (model.LogLoaded.Value) items.Add(new ToolbarLabelView(new("Builds: " + model.BuildCount.Value)).Build());
        items.Add(model.Grade());
        return HStack(items.ToArray()).Spacing(8).Padding(0);
    }
}
