using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.Windows.Media;

namespace GuidedGrade.Services
{
    /// <summary>
    /// Renders background highlights for lines in a text editor that correspond to code violations.
    /// </summary>
    /// <remarks>Use this class to visually indicate lines with violations, such as linting or style errors,
    /// within a text editor control. The highlighted lines are updated by calling SetViolationLines, and highlights can
    /// be cleared with Clear. This renderer is intended for integration with editors that support background rendering
    /// layers.</remarks>
    public class ViolationHighlighter : IBackgroundRenderer
    {
        private readonly TextEditor _editor;
        private readonly List<int> _violationLines = new();

        public KnownLayer Layer => KnownLayer.Background;

        public ViolationHighlighter(TextEditor editor)
        {
            _editor = editor;
        }

        public void SetViolationLines(IEnumerable<int> lines)
        {
            _violationLines.Clear();
            _violationLines.AddRange(lines);
            _editor.TextArea.TextView.InvalidateLayer(Layer);
        }

        public void Clear()
        {
            _violationLines.Clear();
            _editor.TextArea.TextView.InvalidateLayer(Layer);
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_violationLines.Count == 0) return;

            textView.EnsureVisualLines();

            var brush = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0));
            brush.Freeze();

            foreach (var lineNumber in _violationLines)
            {
                if (lineNumber < 1 || lineNumber > _editor.Document.LineCount) continue;

                var line = _editor.Document.GetLineByNumber(lineNumber);
                var segments = BackgroundGeometryBuilder.GetRectsForSegment(textView, line);

                foreach (var rect in segments)
                {
                    // Extend rect to full width of the editor
                    var fullRect = new Rect(0, rect.Top, textView.ActualWidth, rect.Height);
                    drawingContext.DrawRectangle(brush, null, fullRect);
                }
            }
        }
    }
}