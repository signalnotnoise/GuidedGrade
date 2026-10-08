using System.Windows;
using System.Windows.Controls;
using UI_Framework;
using UI_Framework.Wpf;
using GuidedGrade.ViewModels;
namespace GuidedGrade.Views;
internal sealed class ToolbarLabelView(ToolbarLabelViewModel model)
{
 internal View Build() => WpfUI.Native(() => new Border { Height = 32,
  Child = new TextBlock { Text = model.Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } });
}
