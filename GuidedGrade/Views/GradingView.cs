using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using GuidedGrade.Models;
using GuidedGrade.Presentation;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UI_Framework;
using UI_Framework.Wpf;
namespace GuidedGrade.Views;

/// <summary>Declarative grading layout with native HTML editors and score sliders.</summary>
public sealed class GradingView : UserControl, IDisposable
{
    private IHighlightingDefinition? _htmlHighlighting;
    private ViewHost? _host;
    private Student? _student;
    private LabResults? _results;
    private readonly State<int> _revision = new(0);
    private readonly State<string> _name = new("");
    private readonly State<string> _copyLabel = new("Copy Feedback");
    private string _remarks = "", _lastAutoRemarks = "";
    private int _generation;
    private readonly Dictionary<TestResult, string> _pointsDrafts = new();
    private bool _selected, _isBadSubmission, _isAutoZero;
    private System.Windows.Threading.DispatcherTimer? _timer;
    private readonly List<Deduction> _deductions = new()
    {
        new("Method has multiple returns", 10f), new("Method lacks descriptive comments", 10f),
        new("Violates external resource policy", 0f, isAutoZero: true),
        new("Bad submission", 0f, isAutoZero: true, isBadSubmission: true)
    };
    public GradingView()
    {
        Mount();
        Loaded += (_, _) => Mount();
        Unloaded += (_, _) => Dispose();
    }
    private void Mount() { if (_host == null) Content = _host = ReviewTheme.Host(Body); }
    public void Dispose()
    {
        _timer?.Stop(); _timer = null;
        _copyLabel.Value = "Copy Feedback";
        _host?.Dispose(); _host = null; Content = null;
    }
    public LabResults? GetResults() => _results;
    public Student? CurrentStudent => _student;
    public void LoadResults(LabResults? results, Student? student = null)
    {
        _results = results; _student = student; _selected = true; _generation++;
        _pointsDrafts.Clear();
        _isBadSubmission = _isAutoZero = false;
        foreach (var d in _deductions) d.IsApplied = false;
        if (results != null) foreach (var test in results.Results) { test.GradedPoints = test.PointsReceived; _pointsDrafts[test] = test.GradedPoints.ToString("F2"); }
        _name.Value = student?.FirstName ?? "";
        _remarks = _lastAutoRemarks = GetDefaultRemarks(CalculatePercentage());
        Changed();
    }
    public void Clear()
    {
        _pointsDrafts.Clear(); _generation++;
        _results = null; _student = null; _selected = _isBadSubmission = _isAutoZero = false;
        _remarks = _lastAutoRemarks = ""; _name.Value = "";
        foreach (var d in _deductions) d.IsApplied = false;
        Changed();
    }
    private void Changed() => _revision.Value++;
    private View Body()
    {
        _ = _revision.Value;
        if (!_selected) return UI.Text("Select a student and assignment to begin grading.").Padding(24).FontSize(13);
        var possible = _results?.Results.Sum(t => t.PointsTotal) ?? 0;
        var rows = new List<View>
        {
            UI.FlexRow(UI.Text(_results?.Name ?? "Grading").FontSize(18),
                UI.Text($"Total: {CalculateFinalGrade():F2} / {possible:F2}").Foreground(ScoreColor(CalculateFinalGrade(), possible)),
                UI.Button(_copyLabel.Value, CopyFeedback).IsEnabled(_results != null || _isBadSubmission)).Spacing(12).Id("header"),
            UI.Text("Student name").FontSize(12),
            UI.TextField(_name).AccessibilityLabel("Student name").IsEnabled(!_isBadSubmission).Id("student"),
            UI.Text("Initial remarks (HTML)").FontSize(13),
            HtmlEditor(() => _remarks, v => _remarks = v, "Initial remarks").Id("remarks"),
            UI.Text("Deductions").FontSize(14),
            UI.VStack(_deductions.Select(d => UI.Toggle(
                $"{d.Label} — {(d.IsAutoZero ? "Auto zero" : $"-{d.Points:F0} pts")}",
                new UI_Framework.Binding<bool>(() => { _ = _revision.Value; return d.IsApplied; }, v => SetDeduction(d, v)))
                .IsEnabled(!_isBadSubmission || d.IsBadSubmission).Id(d.Label)).ToArray()).Spacing(6).Id("deductions")
        };
        rows.Add(_results == null || _results.Results.Count == 0 ? UI.Text("Results file not found.").FontSize(12) :
            UI.VStack(_results.Results.Select((t, i) => TestCard(t).Id($"test-{_generation}-{i}")).ToArray()).Spacing(12).Id("results"));
        return UI.Scroll(UI.VStack(rows.ToArray()).Spacing(10).Padding(16));
    }
    private void SetDeduction(Deduction deduction, bool applied)
    {
        deduction.IsApplied = applied;
        _isBadSubmission = _deductions.Any(d => d.IsBadSubmission && d.IsApplied);
        _isAutoZero = _deductions.Any(d => d.IsAutoZero && d.IsApplied);
        if (_isBadSubmission)
            _remarks = _lastAutoRemarks = $"<p>Hey {_student?.FirstName ?? "Student"},</p>\n\n" +
                "<p>It looks like the project wasn't submitted correctly. Please send the full project, " +
                "including the Solution (.sln or .slnx) file and all related folders and files. You might " +
                "find it helpful to review the videos in the <em>Required Software</em> and <em>How to Complete " +
                "Labs</em> assignments for more details on submitting your work.</p>\n\n" +
                "<p>I've given you a 24-hour extension so you can resubmit the assignment. Once you've " +
                "submitted the project correctly, I'll review your grade again.</p>\n\n" +
                "<p>Let me know if you have any other questions.</p>\n<p>Doug</p>";
        else if (_isAutoZero)
            _remarks = _lastAutoRemarks = "<p>Unfortunately, this assignment contains the const keyword, C++ references, and " +
                "initializer lists. These are outlined as not being usable within the course within " +
                "the External Research Use Policy. As a result, you will receive a 0 for this assignment.</p>\n\n<p>Doug</p>";
        else if (deduction.IsAutoZero) _remarks = _lastAutoRemarks = GetDefaultRemarks(CalculatePercentage());
        UpdateTotalScore();
    }
    private View TestCard(TestResult test) => UI.VStack(
        UI.Text(test.Name).FontSize(15).Foreground(ScoreColor(test.GradedPoints, test.PointsTotal)),
        UI.Text(string.IsNullOrWhiteSpace(test.Description) ? "No description provided." : test.Description).FontSize(12),
        UI.FlexRow(WpfUI.Native(() => CreateSlider(test), slider =>
            {
                if (Math.Abs(slider.Value - test.GradedPoints) > .005)
                {
                    slider.Tag = true;
                    try { slider.Value = test.GradedPoints; }
                    finally { slider.Tag = null; }
                }
            })
            .AccessibilityLabel($"Score for {test.Name}"),
            UI.TextField(new UI_Framework.Binding<string>(() => { _ = _revision.Value; return _pointsDrafts.GetValueOrDefault(test, test.GradedPoints.ToString("F2")); }, value =>
            {
                _pointsDrafts[test] = value;
                if (float.TryParse(value, out var number) && float.IsFinite(number))
                { test.GradedPoints = Math.Clamp(number, 0, Math.Max(0, test.PointsTotal)); UpdateTotalScore(); }
            })).Width(85).AccessibilityLabel($"Points for {test.Name}"),
            UI.Text($"/ {test.PointsTotal:F2}").FontSize(13)).Spacing(8),
        UI.Text("Comments (HTML)").FontSize(12),
        HtmlEditor(() => test.Comments ?? "", v => test.Comments = v, $"Comments for {test.Name}"))
        .Spacing(8).Padding(12).Background(ReviewTheme.Tokens.Surface).CornerRadius(6).IsEnabled(!_isBadSubmission);
    private Slider CreateSlider(TestResult test)
    {
        var slider = new Slider { Minimum = 0, Maximum = Math.Max(0, test.PointsTotal), Value = test.GradedPoints,
            TickFrequency = Math.Max(0, test.PointsTotal) / 4, VerticalAlignment = VerticalAlignment.Center };
        slider.ValueChanged += (_, e) => { if (slider.Tag is true) return; test.GradedPoints = (float)Math.Round(e.NewValue, 2); _pointsDrafts[test] = test.GradedPoints.ToString("F2"); UpdateTotalScore(); };
        return slider;
    }
    private View HtmlEditor(Func<string> get, Action<string> set, string label) => WpfUI.Native(() =>
    {
        var editor = CreateHtmlEditor(get());
        editor.TextChanged += (_, _) => set(editor.Text);
        return editor;
    }, editor => { if (editor.Text != get()) editor.Text = get(); }).Height(110).AccessibilityLabel(label);
    private static string ScoreColor(float earned, float possible) => possible <= 0 ? "#B8C2CC" :
        earned / possible >= 1 ? "#A6E22E" : earned / possible >= .7 ? "#FD971F" : "#F92672";
    private static string GetDefaultRemarks(float percentage) => percentage switch
    {
        >= 100 => "<p>Excellent job on this assignment!</p>", >= 80 => "<p>Great job on this assignment!</p>",
        >= 70 => "<p>Good job on this assignment!</p>", _ => "<p>Good work on this assignment!</p>"
    };
    private float CalculateFinalGrade() => _isAutoZero || _results == null ? 0 :
        Math.Max(0, _results.Results.Sum(t => t.GradedPoints) - _deductions.Where(d => d.IsApplied && !d.IsAutoZero).Sum(d => d.Points));
    private float CalculatePercentage()
    {
        var possible = _results?.Results.Sum(t => t.PointsTotal) ?? 0;
        return possible > 0 ? _results!.Results.Sum(t => t.GradedPoints) / possible * 100 : 0;
    }
    private void UpdateTotalScore()
    {
        var possible = _results?.Results.Sum(t => t.PointsTotal) ?? 0;
        if (!_isAutoZero && (_remarks == _lastAutoRemarks || string.IsNullOrEmpty(_remarks)))
            _remarks = _lastAutoRemarks = GetDefaultRemarks(possible > 0 ? CalculateFinalGrade() / possible * 100 : 0);
        Changed();
    }
    private void CopyFeedback()
    {
        if (_results == null && !_isBadSubmission) return;
        Clipboard.SetText(GenerateFeedbackHtml()); _copyLabel.Value = "Copied!";
        _timer?.Stop();
        _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => { _copyLabel.Value = "Copy Feedback"; _timer?.Stop(); _timer = null; };
        _timer.Start();
    }
        private ICSharpCode.AvalonEdit.TextEditor CreateHtmlEditor(string initialText = "")
        {
            if (_htmlHighlighting == null)
            {
                var uri = new Uri("pack://application:,,,/GuidedGrade;component/Resources/Html.xshd");
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

        private string GenerateFeedbackHtml()
        {
            if (_isBadSubmission)
                return _remarks;

            var sb = new StringBuilder();
            var possible = _results!.Results.Sum(r => r.PointsTotal);
            var finalGrade = CalculateFinalGrade();
            var remarks = _remarks;

            // Intro
            sb.AppendLine("<p>");
            sb.AppendLine($"    <span class=\"student\">{_name.Value}</span><br><br>");
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
