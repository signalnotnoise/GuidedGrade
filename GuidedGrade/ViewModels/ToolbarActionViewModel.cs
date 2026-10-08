namespace GuidedGrade.ViewModels;
internal sealed record ToolbarActionViewModel(string Label, Action Activate, string AccessibilityLabel, bool Enabled = true, string Color = "#E6EDF5", string? Hint = null);
