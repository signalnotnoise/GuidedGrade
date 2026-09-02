using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Lab_Feedback_WPF.Models;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Lab_Feedback_WPF.Views
{
    public partial class GradingView : UserControl
    {
        private IHighlightingDefinition? _htmlHighlighting;
        private ICSharpCode.AvalonEdit.TextEditor? _remarksEditor;

        private Student? _student;
        private LabResults? _results;
        private CheckBox? _badSubmissionCheckBox;
        private bool _isAutoZero = false;
        private bool _isBadSubmission = false;
        private string _lastAutoRemarks = string.Empty;

        private readonly List<Deduction> _deductions = new()
        {
            new Deduction("Method has multiple returns",        10f),
            new Deduction("Method lacks descriptive comments",  10f),
            new Deduction("Violates external resource policy",   0f, isAutoZero: true),
            new Deduction("Bad submission",                      0f, isAutoZero: true, isBadSubmission: true)
        };

        public GradingView()
        {
            InitializeComponent();
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public void LoadResults(LabResults? results, Student? student = null)
        {
            _isBadSubmission = false;
            _isAutoZero = false;
            _results = results;
            _student = student;
            _lastAutoRemarks = string.Empty;

            foreach (var d in _deductions)
                d.IsApplied = false;

            placeholderPanel.Visibility = Visibility.Collapsed;
            headerPanel.Visibility = Visibility.Visible;
            resultsPanel.Visibility = Visibility.Visible;

            RebuildHeaderControls();
            BuildDeductionsSection();
            SetControlsEnabled(true);

            testCardsPanel.Children.Clear();

            if (results == null || results.Results.Count == 0)
            {
                labTitle.Text = string.Empty;
                totalScore.Text = string.Empty;
                totalPossible.Text = string.Empty;
                noResultsMessage.Visibility = Visibility.Visible;
                resultsDivider.Visibility = Visibility.Collapsed;
                return;
            }

            noResultsMessage.Visibility = Visibility.Collapsed;
            resultsDivider.Visibility = Visibility.Visible;
            labTitle.Text = results.Name;

            foreach (var test in results.Results)
                test.GradedPoints = test.PointsReceived;

            foreach (var test in results.Results)
                testCardsPanel.Children.Add(CreateTestCard(test));

            UpdateTotalScore();
        }

        public void Clear()
        {
            _results = null;
            _student = null;
            _isBadSubmission = false;
            _isAutoZero = false;
            _lastAutoRemarks = string.Empty;

            foreach (var d in _deductions)
                d.IsApplied = false;

            labTitle.Text = string.Empty;
            totalScore.Text = string.Empty;
            totalPossible.Text = string.Empty;

            headerPanel.Visibility = Visibility.Collapsed;
            resultsPanel.Visibility = Visibility.Collapsed;
            placeholderPanel.Visibility = Visibility.Visible;
            placeholderMessage.Text = "Select a student and assignment to begin grading.";
        }

        public LabResults? GetResults() => _results;
        public Student? CurrentStudent => _student;

        // ─── Header Controls ──────────────────────────────────────────────────

        private void RebuildHeaderControls()
        {
            studentNameBox.Text = _student?.FirstName ?? string.Empty;

            var percentage = CalculatePercentage();
            var defaultRemarks = GetDefaultRemarks(percentage);
            _lastAutoRemarks = defaultRemarks;

            if (_remarksEditor == null)
            {
                _remarksEditor = CreateHtmlEditor(defaultRemarks);
                remarksExpander.Content = _remarksEditor;
            }
            else
            {
                _remarksEditor.Text = defaultRemarks;
            }
        }

        private static string GetDefaultRemarks(float percentage) => percentage switch
        {
            >= 100f => "<p>Excellent job on this assignment!</p>",
            >= 80f => "<p>Great job on this assignment!</p>",
            >= 70f => "<p>Good job on this assignment!</p>",
            _ => "<p>Good work on this assignment!</p>"
        };

        // ─── Grade Calculation ────────────────────────────────────────────────

        private float CalculateFinalGrade()
        {
            if (_isAutoZero) return 0f;
            if (_results == null) return 0f;

            var raw = _results.Results.Sum(r => r.GradedPoints);
            var deductions = _deductions
                .Where(d => d.IsApplied && !d.IsAutoZero)
                .Sum(d => d.Points);

            return Math.Max(0f, raw - deductions);
        }

        private float CalculatePercentage()
        {
            if (_results == null) return 0f;
            var earned = _results.Results.Sum(r => r.GradedPoints);
            var possible = _results.Results.Sum(r => r.PointsTotal);
            return possible > 0 ? (earned / possible) * 100f : 0f;
        }

        private void UpdateTotalScore()
        {
            if (_results == null) return;

            // Math
            var possible = _results.Results.Sum(r => r.PointsTotal);
            var earned = CalculateFinalGrade();
            var percentage = possible > 0 ? (earned / possible) * 100f : 0f;

            totalScore.Text = $"{earned:F2}";
            totalPossible.Text = $" / {possible:F2}";

            // Change colors based on score value
            totalScore.Foreground = _isAutoZero
                ? new SolidColorBrush(Color.FromRgb(249, 38, 114))
                : percentage switch
                {
                    >= 100f => new SolidColorBrush(Color.FromRgb(166, 226, 46)),
                    >= 70f => new SolidColorBrush(Color.FromRgb(253, 151, 31)),
                    _ => new SolidColorBrush(Color.FromRgb(249, 38, 114))
                };

            // Don't auto-update remarks when auto zero is active —
            // the remarks are already set to the appropriate message
            if (_isAutoZero || _isBadSubmission) return;

            // Update remarks
            if (_remarksEditor != null)
            {
                var newAutoRemarks = GetDefaultRemarks(percentage);
                var currentText = _remarksEditor.Text;
                var isStillAuto = currentText == _lastAutoRemarks ||
                                  string.IsNullOrEmpty(currentText);

                if (isStillAuto && newAutoRemarks != _lastAutoRemarks)
                {
                    _remarksEditor.Text = newAutoRemarks;
                    _lastAutoRemarks = newAutoRemarks;
                }
            }
        }

        // ─── Deductions ───────────────────────────────────────────────────────

        private void BuildDeductionsSection()
        {
            deductionsPanel.Children.Clear();

            foreach (var deduction in _deductions)
                deductionsPanel.Children.Add(CreateDeductionRow(deduction));
        }

        private UIElement CreateDeductionRow(Deduction deduction)
        {
            // Create container
            var row = new DockPanel { Margin = new Thickness(0, 0, 16, 6) };

            // Create checkbox
            var checkBox = new CheckBox
            {
                IsChecked = deduction.IsApplied,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            // If this deduction is for a bad submission, keep a reference to its checkbox for enabling/disabling
            if (deduction.IsBadSubmission)
                _badSubmissionCheckBox = checkBox;

            // Disable checkbox if it's an auto-zero deduction and a bad submission is already marked
            DockPanel.SetDock(checkBox, Dock.Left);
            row.Children.Add(checkBox);

            // Create points label
            var pointsLabel = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0)
            };

            UpdateDeductionPointsLabel(pointsLabel, deduction);
            DockPanel.SetDock(pointsLabel, Dock.Right);
            row.Children.Add(pointsLabel);

            var label = new TextBlock
            {
                Text = deduction.Label,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(label);

            checkBox.Checked += (s, e) =>
            {
                deduction.IsApplied = true;
                UpdateDeductionPointsLabel(pointsLabel, deduction);

                if (deduction.IsBadSubmission)
                    ApplyBadSubmission();
                else if (deduction.IsAutoZero)
                    ApplyAutoZero();
                else
                    UpdateTotalScore();
            };

            checkBox.Unchecked += (s, e) =>
            {
                deduction.IsApplied = false;
                UpdateDeductionPointsLabel(pointsLabel, deduction);

                if (deduction.IsBadSubmission)
                    RemoveBadSubmission();
                else if (deduction.IsAutoZero)
                    RemoveAutoZero();
                else
                    UpdateTotalScore();
            };

            return row;
        }

        private static void UpdateDeductionPointsLabel(TextBlock label, Deduction deduction)
        {
            if (deduction.IsAutoZero)
            {
                label.Text = deduction.IsApplied ? "AUTO ZERO" : "Auto Zero";
                label.FontWeight = deduction.IsApplied ? FontWeights.Bold : FontWeights.Normal;
            }
            else
            {
                label.Text = $"-{deduction.Points:F0} pts";
            }

            label.Foreground = deduction.IsApplied
                ? new SolidColorBrush(Color.FromRgb(249, 38, 114))
                : new SolidColorBrush(Color.FromRgb(100, 100, 100));
        }

        private void ApplyAutoZero()
        {
            _isAutoZero = true;

            if (_remarksEditor != null)
            {
                var text =
                    "<p>Unfortunately, this assignment contains the const keyword, C++ references, and " +
                    "initializer lists. These are outlined as not being usable within the course within " +
                    "the External Research Use Policy. As a result, you will receive a 0 for this assignment.</p>\n\n" +
                    "<p>Doug</p>";

                _remarksEditor.Text = text;
                _lastAutoRemarks = text;
            }

            UpdateTotalScore();
        }

        private void RemoveAutoZero()
        {
            _isAutoZero = false;

            var percentage = CalculatePercentage();
            var remarks = GetDefaultRemarks(percentage);
            _lastAutoRemarks = remarks;

            if (_remarksEditor != null)
                _remarksEditor.Text = remarks;

            UpdateTotalScore();
        }

        private void ApplyBadSubmission()
        {
            _isBadSubmission = true;
            _isAutoZero = true;

            // Disable controls first
            SetControlsEnabled(false);

            // Then set remarks text — editor must stay enabled to show content
            if (_remarksEditor != null)
            {
                var firstName = _student?.FirstName ?? "Student";
                var text =
                    $"<p>Hey {firstName},</p>\n\n" +
                    "<p>It looks like the project wasn't submitted correctly. Please send the full project, " +
                    "including the Solution (.sln or .slnx) file and all related folders and files. You might " +
                    "find it helpful to review the videos in the <em>Requiured Software</em> and <em>How to Complete " +
                    "Labs</em> assignments for more details on submitting your work.</p>\n\n" +
                    "<p>I've given you a 24-hour extension so you can resubmit the assignment. Once you've " +
                    "submitted the project correctly, I'll review your grade again.</p>\n\n" +
                    "<p>Let me know if you have any other questions.</p>\n" +
                    "<p>Doug</p>";

                _remarksEditor.Text = text;
                _lastAutoRemarks = text;

                // Keep the remarks editor itself enabled so text is visible and editable
                _remarksEditor.IsEnabled = true;
                remarksExpander.IsEnabled = true;
                remarksExpander.IsExpanded = true;
            }

            UpdateTotalScore();
        }

        private void RemoveBadSubmission()
        {
            _isBadSubmission = false;
            _isAutoZero = false;

            var percentage = CalculatePercentage();
            var remarks = GetDefaultRemarks(percentage);
            _lastAutoRemarks = remarks;

            if (_remarksEditor != null)
                _remarksEditor.Text = remarks;

            SetControlsEnabled(true);
            UpdateTotalScore();
        }

        private void SetControlsEnabled(bool enabled)
        {
            studentNameBox.IsEnabled = enabled;
            remarksExpander.IsEnabled = enabled;

            foreach (var child in testCardsPanel.Children.OfType<Border>())
                child.IsEnabled = enabled;

            foreach (var row in deductionsPanel.Children.OfType<DockPanel>())
            {
                var checkBox = row.Children.OfType<CheckBox>().FirstOrDefault();
                if (checkBox == null) continue;

                if (checkBox == _badSubmissionCheckBox)
                {
                    row.IsEnabled = true;
                    checkBox.IsEnabled = true;
                }
                else
                {
                    row.IsEnabled = enabled;
                }
            }
        }

        // ─── Test Cards ───────────────────────────────────────────────────────

        private UIElement CreateTestCard(TestResult test)
        {
            var card = new Border
            {
                //Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
                //BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
                //BorderThickness = new Thickness(1),
                //CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 2, 8),
                Padding = new Thickness(12)
            };

            var stack = new StackPanel();
            card.Child = stack;

            stack.Children.Add(CreateTestCardHeader(test));
            stack.Children.Add(CreateTestCardDescription(test));
            stack.Children.Add(CreateTestCardScoreRow(test));

            return card;
        }

        private UIElement CreateTestCardHeader(TestResult test)
        {
            var headerRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

            var indicator = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = GetIndicatorBrush(test.GradedPoints, test.PointsTotal),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = test // store reference for updates
            };
            DockPanel.SetDock(indicator, Dock.Left);
            headerRow.Children.Add(indicator);

            headerRow.Children.Add(new TextBlock
            {
                Text = test.Name,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 248, 242)),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            return headerRow;
        }

        private static UIElement CreateTestCardDescription(TestResult test)
        {
            var hasDescription = !string.IsNullOrWhiteSpace(test.Description);
            return new TextBlock
            {
                Text = hasDescription ? test.Description : "No description provided.",
                Foreground = new SolidColorBrush(hasDescription
                    ? Color.FromRgb(180, 180, 180)
                    : Color.FromRgb(80, 80, 80)),
                FontSize = 11,
                FontStyle = hasDescription ? FontStyles.Normal : FontStyles.Italic,
                Margin = new Thickness(16, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
        }

        private UIElement CreateTestCardScoreRow(TestResult test)
        {
            var stack = new StackPanel();

            var scoreRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

            var pointsBox = new TextBox
            {
                Width = 60,
                Text = test.GradedPoints.ToString("F2"),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = new SolidColorBrush(Color.FromRgb(248, 248, 242)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
                Padding = new Thickness(4, 2, 4, 2),
                FontFamily = new FontFamily("Consolas"),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(pointsBox, Dock.Right);
            scoreRow.Children.Add(pointsBox);

            scoreRow.Children.Add(new TextBlock
            {
                Text = $" / {test.PointsTotal:F2}",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(scoreRow.Children[^1], Dock.Right);

            var indicator = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = GetIndicatorBrush(test.GradedPoints, test.PointsTotal),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var slider = new Slider
            {
                Minimum = 0,
                Maximum = test.PointsTotal,
                Value = test.GradedPoints,
                TickFrequency = test.PointsTotal / 4,
                IsSnapToTickEnabled = false,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            scoreRow.Children.Add(slider);
            stack.Children.Add(scoreRow);

            slider.ValueChanged += (s, e) =>
            {
                var rounded = (float)Math.Round(e.NewValue, 2);
                test.GradedPoints = rounded;
                pointsBox.Text = rounded.ToString("F2");
                indicator.Background = GetIndicatorBrush(rounded, test.PointsTotal);
                UpdateTotalScore();
            };

            pointsBox.LostFocus += (s, e) =>
            {
                if (float.TryParse(pointsBox.Text, out var value))
                {
                    var clamped = Math.Clamp(value, 0f, test.PointsTotal);
                    test.GradedPoints = clamped;
                    slider.Value = clamped;
                    pointsBox.Text = clamped.ToString("F2");
                    indicator.Background = GetIndicatorBrush(clamped, test.PointsTotal);
                    UpdateTotalScore();
                }
                else
                {
                    pointsBox.Text = test.GradedPoints.ToString("F2");
                }
            };

            var commentsEditor = CreateHtmlEditor(test.Comments);
            commentsEditor.TextArea.Document.Changed += (s, e) =>
                test.Comments = commentsEditor.Text;

            stack.Children.Add(new Expander
            {
                Header = "Comments (HTML)",
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                Margin = new Thickness(0, 4, 0, 0),
                IsExpanded = !string.IsNullOrEmpty(test.Comments),
                Content = commentsEditor
            });

            return stack;
        }

        private static SolidColorBrush GetIndicatorBrush(float graded, float total)
        {
            if (total <= 0) return new SolidColorBrush(Color.FromRgb(100, 100, 100));

            return ((graded / total) * 100f) switch
            {
                >= 100f => new SolidColorBrush(Color.FromRgb(166, 226, 46)),
                >= 70f => new SolidColorBrush(Color.FromRgb(253, 151, 31)),
                _ => new SolidColorBrush(Color.FromRgb(249, 38, 114))
            };
        }

        // ─── HTML Editor ──────────────────────────────────────────────────────

        private ICSharpCode.AvalonEdit.TextEditor CreateHtmlEditor(string initialText = "")
        {
            if (_htmlHighlighting == null)
            {
                var uri = new Uri("pack://application:,,,/Resources/Html.xshd");
                using var stream = Application.GetResourceStream(uri)?.Stream;
                if (stream != null)
                {
                    using var reader = new System.Xml.XmlTextReader(stream);
                    _htmlHighlighting = HighlightingLoader.Load(
                        reader, HighlightingManager.Instance);
                }
            }

            var editor = new ICSharpCode.AvalonEdit.TextEditor
            {
                Text = initialText,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                Foreground = new SolidColorBrush(Color.FromRgb(248, 248, 242)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4),
                MinHeight = 80,
                ShowLineNumbers = false,
                SyntaxHighlighting = _htmlHighlighting,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                WordWrap = true,
                Margin = new Thickness(0, 4, 0, 0)
            };

            editor.TextArea.Background =
                new SolidColorBrush(Color.FromRgb(26, 26, 26));

            return editor;
        }

        // ─── Clipboard ────────────────────────────────────────────────────────

        private void CopyFeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_results == null && !_isBadSubmission) return;

            Clipboard.SetText(GenerateFeedbackHtml());

            copyFeedbackButton.Content = "Copied!";
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (s, args) =>
            {
                copyFeedbackButton.Content = "Copy Feedback";
                timer.Stop();
            };
            timer.Start();
        }

        private string GenerateFeedbackHtml()
        {
            if (_isBadSubmission)
                return _remarksEditor?.Text ?? string.Empty;

            var sb = new StringBuilder();
            var possible = _results!.Results.Sum(r => r.PointsTotal);
            var finalGrade = CalculateFinalGrade();
            var remarks = _remarksEditor?.Text ?? string.Empty;

            // Intro
            sb.AppendLine("<p>");
            sb.AppendLine($"    <span class=\"student\">{studentNameBox.Text}</span><br><br>");
            sb.AppendLine($"    <span class=\"intro\">{remarks}</span>");
            sb.AppendLine("</p>");
            sb.AppendLine();

            // Per-test feedback
            sb.AppendLine("<div class=\"feedback\">");
            foreach (var test in _results.Results)
            {
                var testId = test.Name.ToLower().Replace(" ", "_");
                var passed = test.GradedPoints >= test.PointsTotal;
                var scoreColor = passed ? "rgb(0, 199, 199)" : "rgb(161, 0, 0)";
                var itemColor = passed ? "rgb(74, 145, 57)" : "rgb(151, 151, 151)";
                var hasComments = !string.IsNullOrWhiteSpace(test.Comments);
                var description = string.IsNullOrWhiteSpace(test.Description)
                    ? test.Name : test.Description;

                sb.AppendLine($"<div class=\"{testId}-output\">");
                sb.AppendLine($"    <strong class=\"output-header\">{test.Name}: " +
                              $"<span style=\"color: {scoreColor};\">" +
                              $"{test.GradedPoints:F1}/{test.PointsTotal:F1}</span></strong>");
                sb.AppendLine("    <ul class=\"output-list\">");
                sb.AppendLine($"        <li class=\"{testId}-result\" " +
                              $"style=\"color: {itemColor}; font-weight: {(passed ? "bold" : "normal")}; margin-top: 4px;\">");
                sb.AppendLine($"            <span class=\"feedback\" style=\"display: inline;\">{description}</span>");
                sb.AppendLine($"            <span style=\"display: {(passed ? "inline" : "none")};\"> ✓</span>");
                sb.AppendLine("        </li>");
                sb.AppendLine("    </ul>");
                sb.AppendLine($"    <table style=\"display: {((!passed || hasComments) ? "table" : "none")}; margin-bottom: 12px;\">");
                sb.AppendLine("        <tbody><tr><td>");
                sb.AppendLine(hasComments ? $"            <p>{test.Comments}</p>" : "            <p></p>");
                sb.AppendLine("        </td></tr></tbody>");
                sb.AppendLine("    </table>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");
            sb.AppendLine();

            // Deductions
            var appliedDeductions = _deductions.Where(d => d.IsApplied).ToList();
            if (appliedDeductions.Any())
            {
                sb.AppendLine("<ul>");
                foreach (var d in appliedDeductions)
                    sb.AppendLine(d.IsAutoZero
                        ? $"    <li><strong>-100pts:</strong> {d.Label}</li>"
                        : $"    <li><strong>-{d.Points:F0}pts:</strong> {d.Label}</li>");
                sb.AppendLine("</ul>");
            }

            // Final grade
            sb.AppendLine($"<p>Final Grade: <span class=\"grade\">{finalGrade:F1}</span>/{possible:F0}</p>");

            return sb.ToString();
        }
    }
}