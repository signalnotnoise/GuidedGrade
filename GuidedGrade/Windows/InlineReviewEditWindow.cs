using GuidedGrade.Models;
using GuidedGrade.Presentation;
using GuidedGrade.ViewModels;
using GuidedGrade.Views;
namespace GuidedGrade.Windows;
internal sealed class InlineReviewEditWindow : ReviewWindow
{
    private readonly InlineReviewEditViewModel _model;
    internal string? DestinationFile => _model.DestinationFile;
    internal InlineReviewEditWindow(SectionFeedback feedback, int lineCount, double maximum, IReadOnlyList<string>? files = null, Func<string,int>? lineCounter = null)
    {
        Title = "Modify inline review"; Width = 600; Height = 500;
        var model = _model = new InlineReviewEditViewModel(feedback, lineCount, maximum, files, lineCounter);
        ShowView(() => new InlineReviewEditView(model, () => { if (model.Apply()) DialogResult = true; }, () => DialogResult = false).Build());
    }
}
