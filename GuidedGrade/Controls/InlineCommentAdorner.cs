using System.Windows;
using System.Windows.Controls;
using GuidedGrade.Models;
using GuidedGrade.Presentation;
using UI_Framework.Wpf;

namespace GuidedGrade.Controls;

public sealed class InlineCommentAdorner : ContentControl, IDisposable
{
    private readonly SectionFeedback _feedback;
    private readonly bool _earlierReview;
    private readonly GuidedGrade.ViewModels.InlineCommentViewModel _model;
    private readonly ViewHost _host;
    public int LineNumber { get; private set; }
    public SectionFeedback Feedback => _feedback;
    public bool IsExpanded { get => _model.Expanded.Value; set => _model.Expanded.Value = value; }
    public event EventHandler<SectionFeedback>? ApproveRequested;
    public event EventHandler<SectionFeedback>? ModifyRequested;
    public event EventHandler<SectionFeedback>? PlacementChanged;
    internal const string DragFormat = "GuidedGrade.InlineCommentPlacement";
    public event EventHandler<SectionFeedback>? RejectRequested;

    public InlineCommentAdorner(SectionFeedback feedback, int lineNumber, bool earlierReview = false)
    {
        _earlierReview = earlierReview;
        _feedback = feedback; LineNumber = lineNumber;
        _model = new(feedback, earlierReview,
            () => ApproveRequested?.Invoke(this, feedback), () => ModifyRequested?.Invoke(this, feedback),
            () => RejectRequested?.Invoke(this, feedback), TogglePin,
            source => DragDrop.DoDragDrop(source, new DataObject(DragFormat, this), DragDropEffects.Move));
        MaxWidth = 480; Margin = new Thickness(8, 2, 8, 2);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Content = _host = ReviewTheme.Host(new GuidedGrade.Views.InlineCommentView(_model).Build);
    }

    private void TogglePin()
    {
        GuidedGrade.Services.CommentPlacementService.TogglePin(_feedback);
        PlacementChanged?.Invoke(this, _feedback);
    }
    internal bool MoveTo(int line, int lineCount)
    {
        if (_earlierReview || !GuidedGrade.Services.CommentPlacementService.Move(_feedback, line, lineCount)) return false;
        LineNumber = line;
        PlacementChanged?.Invoke(this, _feedback);
        return true;
    }
    public void MarkApproved() => _model.MarkApproved();
    public void Dispose() { _host.Dispose(); Content = null; }
}
