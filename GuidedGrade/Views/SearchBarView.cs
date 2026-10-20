using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;

namespace GuidedGrade.Views;

internal sealed class SearchBarView(SearchBarViewModel model)
{
    internal View Build() => HStack(
        Text("⌕").FontSize(22).Foreground(ReviewTheme.Tokens.Muted),
        TextField(model.Query).Width(210).AccessibilityLabel("Search query"),
        Button("↑", model.Previous).ButtonStyle(ButtonStyleKind.Quiet).AccessibilityLabel("Previous match (Shift+Enter)").Width(32),
        Button("↓", model.Next).ButtonStyle(ButtonStyleKind.Quiet).AccessibilityLabel("Next match (Enter)").Width(32),
        Button("×", model.Close).ButtonStyle(ButtonStyleKind.Quiet).AccessibilityLabel("Close search (Escape)").Width(32)
    ).Spacing(4).Padding(8).Background(ReviewTheme.Tokens.Surface).CornerRadius(8).Height(60);
}
