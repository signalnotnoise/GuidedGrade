using System.Windows.Input;
using GuidedGrade.ViewModels;

namespace GuidedGrade;

public partial class MainWindow
{
    private readonly DocumentSearchViewModel _sourceSearch;
    private bool _lastSearchWasLog;

    private void InitializeDocumentSearch()
    {
        codeEditor.PreviewMouseDown += (_, _) => _lastSearchWasLog = false;
        _logModel.Panel.PreviewMouseDown += (_, _) => _lastSearchWasLog = true;
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key != Key.F || Keyboard.Modifiers != ModifierKeys.Control) return;
            var log = _logModel.Panel.IsKeyboardFocusWithin ||
                (!codeEditor.IsKeyboardFocusWithin && _lastSearchWasLog && _logSelected.Value);
            (log ? _logModel.Search : _sourceSearch).Open();
            args.Handled = true;
        };
        Closed += (_, _) => { _sourceSearch.Dispose(); _logModel.Search.Dispose(); };
    }
}
