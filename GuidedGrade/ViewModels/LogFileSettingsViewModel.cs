using UI_Framework;
using GuidedGrade.Services;
namespace GuidedGrade.ViewModels;
internal sealed class LogFileSettingsViewModel(WorkspacePanelPreferences preferences)
{
    internal State<string> Name { get; } = new(preferences.LogFileName);
    internal State<string> Extension { get; } = new(preferences.LogFileExtension);
}
