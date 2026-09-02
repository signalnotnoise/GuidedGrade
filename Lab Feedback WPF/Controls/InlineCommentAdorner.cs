using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Document;
using Lab_Feedback_WPF.Models;

namespace Lab_Feedback_WPF.Controls
{
    /// <summary>
    /// Displays inline expandable feedback comments in the code editor
    /// </summary>
    public class InlineCommentAdorner : FrameworkElement
    {
        private readonly SectionFeedback _feedback;
        private readonly int _lineNumber;
        private bool _isExpanded = false;
        private Border _commentBorder;
        private StackPanel _commentPanel;
        private TextBlock _headerText;

        public InlineCommentAdorner(SectionFeedback feedback, int lineNumber)
        {
            _feedback = feedback;
            _lineNumber = lineNumber;
            BuildUI();
            _isExpanded = true;
            UpdateExpandedState();
        }

        public int LineNumber => _lineNumber;
        public SectionFeedback Feedback => _feedback;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                UpdateExpandedState();
            }
        }

        private void BuildUI()
        {
            _commentPanel = new StackPanel { Margin = new Thickness(5) };

            // Header (always visible)
            var headerPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };

            var expandButton = new Button
            {
                Content = "▼",
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                Cursor = Cursors.Hand
            };
            expandButton.Click += (s, e) => IsExpanded = !IsExpanded;
            DockPanel.SetDock(expandButton, Dock.Left);
            headerPanel.Children.Add(expandButton);

            headerPanel.Cursor = Cursors.Hand;
            headerPanel.MouseLeftButtonDown += (_, _) => IsExpanded = !IsExpanded;

            _headerText = new TextBlock
            {
                Text = BuildHeaderText(),
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176)),
                Margin = new Thickness(5, 0, 0, 0)
            };
            headerPanel.Children.Add(_headerText);

            _commentPanel.Children.Add(headerPanel);

            // Expandable content (hidden by default)
            var contentPanel = new StackPanel { Name = "ContentPanel", Visibility = Visibility.Collapsed };

            // Strengths
            if (_feedback.Strengths?.Count > 0)
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = "✓ Strengths:",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176)),
                    Margin = new Thickness(0, 5, 0, 2)
                });

                foreach (var strength in _feedback.Strengths)
                {
                    contentPanel.Children.Add(new TextBlock
                    {
                        Text = $"  • {strength}",
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(10, 0, 0, 2)
                    });
                }
            }

            // Issues
            if (_feedback.Issues?.Count > 0)
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = "⚠ Issues:",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(252, 207, 49)),
                    Margin = new Thickness(0, 8, 0, 2)
                });

                foreach (var issue in _feedback.Issues)
                {
                    contentPanel.Children.Add(new TextBlock
                    {
                        Text = $"  • {issue}",
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(10, 0, 0, 2)
                    });
                }
            }

            // Suggested code
            if (!string.IsNullOrWhiteSpace(_feedback.SuggestedCode))
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = "📝 Suggested Fix:",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(86, 156, 214)),
                    Margin = new Thickness(0, 8, 0, 2)
                });

                var codeBox = new TextBox
                {
                    Text = _feedback.SuggestedCode,
                    IsReadOnly = true,
                    FontFamily = new FontFamily("Consolas"),
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(5),
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 200,
                    Margin = new Thickness(10, 2, 0, 5)
                };
                contentPanel.Children.Add(codeBox);
            }

            // Explanation
            if (!string.IsNullOrWhiteSpace(_feedback.Explanation))
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = "💬 Explanation:",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(206, 145, 120)),
                    Margin = new Thickness(0, 8, 0, 2)
                });

                contentPanel.Children.Add(new TextBlock
                {
                    Text = _feedback.Explanation,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 0, 0, 5)
                });
            }

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var approveButton = CreateActionButton("Approve", 80, Color.FromRgb(78, 201, 176));
            approveButton.Click += ApproveButton_Click;
            buttonPanel.Children.Add(approveButton);

            var regenerateButton = CreateActionButton("Regenerate", 95, Color.FromRgb(14, 99, 156));
            regenerateButton.Click += RegenerateButton_Click;
            buttonPanel.Children.Add(regenerateButton);

            var rejectButton = CreateActionButton("Reject", 70, Color.FromRgb(196, 80, 80));
            rejectButton.Click += RejectButton_Click;
            buttonPanel.Children.Add(rejectButton);

            if (_feedback.ReviewStatus == FeedbackReviewStatus.Approved)
            {
                buttonPanel.Visibility = Visibility.Collapsed;
            }

            contentPanel.Children.Add(buttonPanel);

            _commentPanel.Children.Add(contentPanel);

            // Wrap in border with highlight
            _commentBorder = new Border
            {
                Child = _commentPanel,
                Background = new SolidColorBrush(Color.FromArgb(230, 45, 45, 48)),
                BorderBrush = new SolidColorBrush(_feedback.ReviewStatus == FeedbackReviewStatus.Approved
                    ? Color.FromRgb(166, 226, 46)
                    : Color.FromRgb(78, 201, 176)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                MaxWidth = 600,
                Margin = new Thickness(20, 2, 20, 2)
            };

            AddVisualChild(_commentBorder);
            AddLogicalChild(_commentBorder);
        }

        private void UpdateExpandedState()
        {
            var expandButton = (Button)((DockPanel)_commentPanel.Children[0]).Children[0];
            expandButton.Content = _isExpanded ? "▼" : "▶";

            var contentPanel = (StackPanel)_commentPanel.Children[1];
            contentPanel.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;

            InvalidateMeasure();
        }

        public event EventHandler<SectionFeedback>? ApproveRequested;
        public event EventHandler<SectionFeedback>? RegenerateRequested;
        public event EventHandler<SectionFeedback>? RejectRequested;

        public void MarkApproved()
        {
            _feedback.ReviewStatus = FeedbackReviewStatus.Approved;
            _headerText.Text = BuildHeaderText();
            _commentBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(166, 226, 46));

            if (_commentPanel.Children[1] is StackPanel contentPanel)
            {
                foreach (var child in contentPanel.Children)
                {
                    if (child is StackPanel { Orientation: Orientation.Horizontal } buttonPanel)
                    {
                        buttonPanel.Visibility = Visibility.Collapsed;
                        break;
                    }
                }
            }

            InvalidateMeasure();
        }

        private string BuildHeaderText()
        {
            var status = _feedback.ReviewStatus == FeedbackReviewStatus.Approved ? "Approved" : "Review";
            return $"💡 {status}: {_feedback.SectionName} - Score: {_feedback.SuggestedScore} pts";
        }

        private static Button CreateActionButton(string content, double width, Color background)
        {
            return new Button
            {
                Content = content,
                Width = width,
                Height = 25,
                Background = new SolidColorBrush(background),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 0)
            };
        }

        private void ApproveButton_Click(object sender, RoutedEventArgs e)
        {
            ApproveRequested?.Invoke(this, _feedback);
        }

        private void RegenerateButton_Click(object sender, RoutedEventArgs e)
        {
            RegenerateRequested?.Invoke(this, _feedback);
        }

        private void RejectButton_Click(object sender, RoutedEventArgs e)
        {
            RejectRequested?.Invoke(this, _feedback);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _commentBorder;

        protected override Size MeasureOverride(Size availableSize)
        {
            _commentBorder.Measure(availableSize);
            return _commentBorder.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _commentBorder.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}
