using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.ViewModels;
namespace GuidedGrade.Views;
internal sealed class StudentsPanelView(StudentsPanelViewModel model)
{ internal View Build() => WpfUI.Native(() => model.Control).Id("students"); }
