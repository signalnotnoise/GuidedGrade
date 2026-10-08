using UI_Framework;
using GuidedGrade.ViewModels;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class GradeBadgeView(GradeBadgeViewModel model)
{ internal View Build() => Text(model.Text).FontSize(12).Foreground(model.Color).Padding(6).Background(model.Background); }
