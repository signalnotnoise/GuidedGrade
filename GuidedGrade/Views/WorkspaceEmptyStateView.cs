using UI_Framework;
using GuidedGrade.ViewModels;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class WorkspaceEmptyStateView(WorkspaceEmptyStateViewModel model)
{ internal View Build() => VStack(Text(model.Title).FontSize(22), Text(model.Instructions)).Spacing(12).Padding(24); }
