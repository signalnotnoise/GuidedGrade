using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;
using GuidedGrade.Models;

namespace GuidedGrade.Controls
{
    /// <summary>
    /// Manager for inline comment adorners in AvalonEdit
    /// </summary>
    public class InlineCommentLayer : Canvas
    {
        private readonly TextEditor _editor;
        private readonly List<InlineCommentAdorner> _comments = new();

        public InlineCommentLayer(TextEditor editor)
        {
            _editor = editor;
            ClipToBounds = false;
            IsHitTestVisible = true;
            Background = null;

            _editor.TextArea.AllowDrop = true;
            _editor.TextArea.PreviewDragOver += Editor_DragOver;
            _editor.TextArea.PreviewDrop += Editor_Drop;
            _editor.SizeChanged += (_, _) => UpdateCommentPositions();
            _editor.TextArea.TextView.VisualLinesChanged += (_, _) => UpdateCommentPositions();
            _editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => UpdateCommentPositions();
        }

        public void AddComment(SectionFeedback feedback, int startLine, int endLine, bool earlierReview = false)
        {
            var lineCount = _editor.Document?.LineCount ?? 0;
            var preferredLine = startLine > 0 ? startLine : endLine;
            var anchorLine = lineCount <= 0
                ? Math.Max(1, preferredLine)
                : Math.Clamp(preferredLine, 1, lineCount);

            var adorner = new InlineCommentAdorner(feedback, anchorLine, earlierReview);
            adorner.ApproveRequested += Adorner_ApproveRequested;
            adorner.ModifyRequested += Adorner_ModifyRequested;
            adorner.PlacementChanged += Adorner_PlacementChanged;
            adorner.RejectRequested += Adorner_RejectRequested;

            _comments.Add(adorner);
            Children.Add(adorner);

            UpdateCommentPositions();
        }

        public void RemoveComment(InlineCommentAdorner adorner)
        {
            adorner.ApproveRequested -= Adorner_ApproveRequested;
            adorner.ModifyRequested -= Adorner_ModifyRequested;
            adorner.PlacementChanged -= Adorner_PlacementChanged;
            adorner.RejectRequested -= Adorner_RejectRequested;

            _comments.Remove(adorner);
            Children.Remove(adorner);
            adorner.Dispose();

            UpdateCommentPositions();
        }

        public void ClearComments()
        {
            foreach (var comment in _comments.ToList())
            {
                RemoveComment(comment);
            }
        }

        public void UpdateCommentPositions()
        {
            if (_comments.Count == 0 || _editor.Document == null)
                return;

            var textView = _editor.TextArea.TextView;
            var maxWidth = Math.Max(_editor.ActualWidth - 60, 260);
            var lineCount = _editor.Document.LineCount;

            var bottom = double.NegativeInfinity;
            foreach (var comment in _comments.OrderBy(c => c.LineNumber))
            {
                if (comment.LineNumber < 1 || comment.LineNumber > lineCount)
                {
                    comment.Visibility = Visibility.Collapsed;
                    continue;
                }

                var line = _editor.Document.GetLineByNumber(comment.LineNumber);
                var visualLine = textView.GetVisualLine(line.LineNumber);
                // Overall reviews describe the whole file, so keep their card
                // reachable even when the first source line is scrolled away.
                if (visualLine == null && comment.Feedback.IsOverallReview && !comment.Feedback.IsPinned && textView.VisualLinesValid)
                    visualLine = textView.VisualLines.FirstOrDefault();

                if (visualLine == null)
                {
                    comment.Visibility = Visibility.Collapsed;
                    continue;
                }

                var x = 20.0;
                var y = Math.Max(visualLine.VisualTop - textView.ScrollOffset.Y + visualLine.Height + 4, bottom + 4);

                comment.Visibility = Visibility.Visible;
                comment.Measure(new Size(maxWidth, double.PositiveInfinity));
                var width = Math.Min(comment.DesiredSize.Width, maxWidth);
                var height = Math.Min(comment.DesiredSize.Height, 400);
                bottom = y + height;

                comment.Arrange(new Rect(x, y, width, height));
                Canvas.SetLeft(comment, x);
                Canvas.SetTop(comment, y);
                comment.Visibility = Visibility.Visible;
            }
        }

        private InlineCommentAdorner? DraggedCard(DragEventArgs e) => e.Data.GetDataPresent(InlineCommentAdorner.DragFormat)
            && e.Data.GetData(InlineCommentAdorner.DragFormat) is InlineCommentAdorner card && _comments.Contains(card) && !card.Feedback.IsPinned ? card : null;
        private void Editor_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(InlineCommentAdorner.DragFormat)) return;
            var card = DraggedCard(e);
            var point = e.GetPosition(_editor);
            // Let the user reach code outside the visible viewport while holding the handle.
            if (point.Y < 30) _editor.ScrollToVerticalOffset(Math.Max(0, _editor.VerticalOffset - 20));
            else if (point.Y > _editor.ActualHeight - 30) _editor.ScrollToVerticalOffset(_editor.VerticalOffset + 20);
            var position = _editor.GetPositionFromPoint(point);
            if (card != null && position is { } target) _editor.TextArea.Caret.Line = target.Line;
            e.Effects = card != null && position != null ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }
        private void Editor_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(InlineCommentAdorner.DragFormat)) return;
            var card = DraggedCard(e);
            var position = _editor.GetPositionFromPoint(e.GetPosition(_editor));
            if (card != null && position is { } target) card.MoveTo(target.Line, _editor.Document.LineCount);
            e.Handled = true;
        }
        public event EventHandler<SectionFeedback>? PlacementChanged;
        private void Adorner_PlacementChanged(object? sender, SectionFeedback feedback) => PlacementChanged?.Invoke(this, feedback);
        public event EventHandler<SectionFeedback>? ApproveRequested;
        public event EventHandler<SectionFeedback>? RegenerateRequested;
        public event EventHandler<SectionFeedback>? ModifyRequested;
        private void Adorner_ModifyRequested(object? sender, SectionFeedback feedback) => ModifyRequested?.Invoke(this, feedback);
        public event EventHandler<SectionFeedback>? RejectRequested;

        private void Adorner_ApproveRequested(object? sender, SectionFeedback feedback)
        {
            if (sender is InlineCommentAdorner adorner)
                adorner.MarkApproved();

            ApproveRequested?.Invoke(this, feedback);
        }

        private void Adorner_RegenerateRequested(object? sender, SectionFeedback feedback)
        {
            RegenerateRequested?.Invoke(this, feedback);
        }

        private void Adorner_RejectRequested(object? sender, SectionFeedback feedback)
        {
            if (sender is InlineCommentAdorner adorner)
                RemoveComment(adorner);

            RejectRequested?.Invoke(this, feedback);
        }
    }
}
