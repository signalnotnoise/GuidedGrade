namespace GuidedGrade.ViewModels;
internal sealed class NavigationTabViewModel(string label, bool selected, Action activate, string accessibilityLabel)
{
 internal string Label { get; } = label;
 internal bool Selected { get; } = selected;
 internal Action Activate { get; } = activate;
 internal string AccessibilityLabel { get; } = accessibilityLabel;
}
