using UI_Framework;
using GuidedGrade.ViewModels;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class LogFileSettingsView(LogFileSettingsViewModel model)
{
    internal View Build() => VStack(Text("Source history logs"),
        Text("Filename without extension (* matches any name)"), TextField(model.Name).AccessibilityLabel("Log filename"),
        Text("File extension"), TextField(model.Extension).AccessibilityLabel("Log file extension"),
        Text("Matching files open in Logs when selected. The contents must use the supported source-history log format.").FontSize(12)
    ).Spacing(4);
}
