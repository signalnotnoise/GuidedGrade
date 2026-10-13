using System.Globalization;
using GuidedGrade.Models;
using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class InlineReviewEditViewModel
{
    private readonly SectionFeedback _feedback;
    private readonly int _lineCount;
    private readonly double _maximum;
    private readonly Func<string,int>? _lineCounter;
    internal IReadOnlyList<string> Files { get; }
    internal State<int> FileIndex { get; } = new(0);
    internal string? DestinationFile => Files.Count == 0 ? null : FileIndex.Value >= 0 && FileIndex.Value < Files.Count ? Files[FileIndex.Value] : null;
    internal State<string> Comment { get; }
    internal State<string> Points { get; }
    internal State<string> Line { get; }
    internal State<string> Error { get; } = new("");
    internal string Title => _feedback.SectionName;
    internal double Maximum => _maximum;
    internal bool CanMove => !_feedback.IsPinned;
    internal string PointsLabel => _feedback.RubricReview?.IsDeduction == true ? "Points deducted" : "Points awarded";
    internal InlineReviewEditViewModel(SectionFeedback feedback, int lineCount, double maximum, IReadOnlyList<string>? files = null, Func<string,int>? lineCounter = null)
    {
        _feedback = feedback; _lineCount = lineCount; _maximum = maximum; Files = files ?? []; _lineCounter=lineCounter;
        Comment = new(feedback.Explanation); Points = new(feedback.SuggestedScore.ToString(CultureInfo.InvariantCulture));
        Line = new(feedback.StartLine.ToString(CultureInfo.InvariantCulture));
    }
    internal bool Apply()
    {
        if (string.IsNullOrWhiteSpace(Comment.Value)) { Error.Value = "Enter your comment."; return false; }
        if (!double.TryParse(Points.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var points) ||
            !double.IsFinite(points) || points < 0 || points > _maximum)
        { Error.Value = $"Award points from 0 to {_maximum}."; return false; }
        if (Files.Count > 0 && DestinationFile == null) { Error.Value = "Choose a destination file."; return false; }
        if (_feedback.IsPinned && FileIndex.Value != 0) { Error.Value = "Unpin the comment before changing its file."; return false; }
        int lineCount;
        try { lineCount = DestinationFile != null && _lineCounter != null ? _lineCounter(DestinationFile) : _lineCount; }
        catch (Exception ex) { Error.Value = "Could not read destination: " + ex.Message; return false; }
        if (!int.TryParse(Line.Value, out var line) || line < 1 || line > lineCount)
        { Error.Value = $"Choose a line from 1 to {lineCount}."; return false; }
        if (_feedback.IsPinned && line != _feedback.StartLine) { Error.Value = "Unpin the comment before changing its code line."; return false; }
        _feedback.Strengths.Clear(); _feedback.Issues.Clear(); _feedback.SuggestedCode = "";
        _feedback.Explanation = Comment.Value; _feedback.SuggestedScore = points; if (!_feedback.IsPinned) _feedback.StartLine = _feedback.EndLine = line;
        if (_feedback.RubricReview != null) _feedback.RubricReview.LocationResolved = true;
        Error.Value = ""; return true;
    }
}
