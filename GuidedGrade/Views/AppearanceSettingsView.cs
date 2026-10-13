using GuidedGrade.ViewModels;
using UI_Framework;
using static UI_Framework.UI;
namespace GuidedGrade.Views;
internal sealed class AppearanceSettingsView(AppearanceSettingsViewModel model)
{
    internal View Build() => VStack(Text("Appearance"),
        Picker(new[] { "Dark", "Light" }, model.Mode).AccessibilityLabel("Color theme")).Spacing(4);
}
