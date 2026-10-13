using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class AppearanceSettingsViewModel(bool lightMode)
{
    internal State<int> Mode { get; } = new(lightMode ? 1 : 0);
    internal bool LightMode => Mode.Value == 1;
}
