namespace GuidedGrade.ViewModels;
internal sealed class MenuSelectorViewModel(string label, string[] items, int selectedIndex, Action<int> select, string accessibilityLabel)
{
 internal string Label { get; } = label;
 internal string[] Items { get; } = items;
 internal int SelectedIndex { get; } = selectedIndex;
 internal Action<int> Select { get; } = select;
 internal string AccessibilityLabel { get; } = accessibilityLabel;
 internal string Header => Label + ": " + (SelectedIndex >= 0 && SelectedIndex < Items.Length ? Items[SelectedIndex] : "Choose");
 internal string Identity => AccessibilityLabel + System.Text.Json.JsonSerializer.Serialize(new { Items, SelectedIndex });
}
