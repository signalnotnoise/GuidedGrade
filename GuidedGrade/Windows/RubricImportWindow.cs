using GuidedGrade.Models;
using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using GuidedGrade.Views;
namespace GuidedGrade.Windows;
internal sealed class RubricImportWindow : ReviewWindow
{
    private readonly RubricImportViewModel _model = new();
    internal List<RubricItem> Items { get; } = new();
    internal RubricImportWindow()
    {
        Title = "Import rubric"; Width = 620; Height = 560; MinWidth = 400; MinHeight = 380;
        var view = new RubricImportView(_model, Import, () => DialogResult = false); ShowView(view.Build);
    }
    private void Import()
    {
        try { Items.AddRange(_model.Parse()); DialogResult = true; }
        catch (Exception ex) { _model.Error.Value = ex.Message; }
    }
}
