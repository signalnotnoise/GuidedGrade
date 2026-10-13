using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.ViewModels;
namespace GuidedGrade.Views;
internal sealed class SubmissionFilesPanelView(SubmissionFilesPanelViewModel model)
{ internal View Build() => WpfUI.Native(() => model.Control).Id("files"); }
