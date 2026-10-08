using UI_Framework;
using GuidedGrade.ViewModels;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class PanelHeaderView(PanelHeaderViewModel model)
{
 internal View Build() => FlexRow(Text(model.Title).FontSize(16).Flex(1), Button("×", model.Close).AccessibilityLabel("Close review panel").Flex(0)).Spacing(8).Padding(8);
}
