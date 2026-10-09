using System.Windows;
using System.Windows.Controls;
using GuidedGrade.Models;
using GuidedGrade.Presentation;
using UI_Framework;
using UI_Framework.Wpf;
using static UI_Framework.UI;

namespace GuidedGrade.Controls;

public sealed class InlineCommentAdorner : ContentControl, IDisposable
{
    private readonly SectionFeedback _feedback;
    private readonly bool _earlierReview;
    private readonly State<bool> _expanded = new(false);
    private readonly State<bool> _approved;
    private readonly ViewHost _host;
    public int LineNumber { get; private set; }
    public SectionFeedback Feedback => _feedback;
    public bool IsExpanded { get => _expanded.Value; set => _expanded.Value = value; }
    public event EventHandler<SectionFeedback>? ApproveRequested;
    public event EventHandler<SectionFeedback>? ModifyRequested;
    public event EventHandler<SectionFeedback>? PlacementChanged;
    internal const string DragFormat = "GuidedGrade.InlineCommentPlacement";
    public event EventHandler<SectionFeedback>? RejectRequested;

    public InlineCommentAdorner(SectionFeedback feedback, int lineNumber, bool earlierReview = false)
    {
        _earlierReview = earlierReview;
        _feedback = feedback; LineNumber = lineNumber;
        _approved = new(feedback.ReviewStatus == FeedbackReviewStatus.Approved);
        MaxWidth = 480; Margin = new Thickness(8, 2, 8, 2);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Content = _host = ReviewTheme.Host(BuildView);
    }

    private View BuildView()
    {
        var children = new List<View>
        {
            HStack(new GuidedGrade.Views.CommentPlacementView(new GuidedGrade.ViewModels.CommentPlacementViewModel(
                _feedback.IsPinned, !_earlierReview, source => DragDrop.DoDragDrop(source, new DataObject(DragFormat, this), DragDropEffects.Move), TogglePin)).Build(),
            Button($"{(_expanded.Value ? "−" : "+")} {(_approved.Value ? "Approved" : "Review")}: {_feedback.SectionName}{(_feedback.IsOverallReview ? "" : $" · {_feedback.SuggestedScore} pts")}",
                () => _expanded.Value = !_expanded.Value).ButtonStyle(ButtonStyleKind.Quiet).Id("header")).Spacing(4)
        };
        if (_earlierReview)
            children.Add(Text("Earlier review · assignment not recorded").FontSize(12).Foreground("#FCCF31"));
        if (_feedback.RubricReview is { LocationResolved: false })
            children.Add(Text("Location needs review — Modify to choose a line.").FontSize(12).Foreground("#FCCF31"));
        if (_expanded.Value)
        {
            if (_feedback.Strengths.Count > 0)
                children.Add(VStack(Text("Strengths").Foreground("#4EC9B0"), VStack(_feedback.Strengths.Select(value => Text("• " + value)).ToArray()).Spacing(4)).Spacing(6).Id("strengths"));
            if (_feedback.Issues.Count > 0)
                children.Add(VStack(Text("Issues").Foreground("#FCCF31"), VStack(_feedback.Issues.Select(value => Text("• " + value)).ToArray()).Spacing(4)).Spacing(6).Id("issues"));
            if (!string.IsNullOrWhiteSpace(_feedback.SuggestedCode))
                children.Add(VStack(Text("Suggested fix").Foreground("#569CD6"),
                    TextEditor(new Binding<string>(() => _feedback.SuggestedCode, _ => { })).IsReadOnly(true).UndoLimit(0).Height(160).AccessibilityLabel("Suggested code")
                ).Spacing(6).Id("code"));
            if (!string.IsNullOrWhiteSpace(_feedback.Explanation))
                children.Add(VStack(Text("Explanation").Foreground("#CE9178"), Text(_feedback.Explanation)).Spacing(6).Id("explanation"));
            if (!_earlierReview)
                children.Add(AdaptiveGrid(100,
                    Button("Approve", () => ApproveRequested?.Invoke(this, _feedback)).AccessibilityLabel("Approve inline comment").ButtonStyle(ButtonStyleKind.Primary).IsEnabled(!_approved.Value && _feedback.RubricReview is not { LocationResolved: false }),
                    Button("Modify", () => ModifyRequested?.Invoke(this, _feedback)).AccessibilityLabel("Modify inline comment"),
                    Button("Reject", () => RejectRequested?.Invoke(this, _feedback)).AccessibilityLabel("Reject inline comment")).Spacing(8).Id("actions"));
        }
        // The editor overlay caps cards at 400px. Scroll long reviews instead of clipping actions.
        var content = _expanded.Value
            ? new[] { children[0], Scroll(VStack(children.Skip(1).ToArray()).Spacing(10)).Height(290).Id("details") }
            : children.ToArray();
        return VStack(content).Spacing(8).Padding(6).Background(ReviewTheme.Tokens.Surface).CornerRadius(6);
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
    public void MarkApproved() { _feedback.ReviewStatus = FeedbackReviewStatus.Approved; _approved.Value = true; _expanded.Value = false; }
    public void Dispose() { _host.Dispose(); Content = null; }
}
