namespace GuidedGrade.ViewModels;
internal sealed class ReviewMenuViewModel(Func<bool> hasSelection, Func<bool> hasFiles, Action batch, Action selection, Action overall, Action clear, Action clearAll)
{
 internal Func<bool> HasSelection { get; } = hasSelection;
 internal Func<bool> HasFiles { get; } = hasFiles;
 internal Action Batch { get; } = batch;
 internal Action Selection { get; } = selection;
 internal Action Overall { get; } = overall;
 internal Action Clear { get; } = clear;
 internal Action ClearAll { get; } = clearAll;
}
