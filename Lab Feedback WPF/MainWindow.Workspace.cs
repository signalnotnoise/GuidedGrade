using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Lab_Feedback_WPF;

public partial class MainWindow
{
    private readonly Dictionary<string, string> _reviewDrafts = new();
    private readonly HashSet<string> _restoredFeedback = new(StringComparer.Ordinal);
    private string FeedbackIdentity(string filePath, Models.SectionFeedback feedback) =>
        CurrentFeedbackKey() + "|" + filePath + "|" + feedback.StartLine + "|" + feedback.EndLine + "|" + feedback.SectionName;
    private void MarkFeedbackImported(string filePath, Models.SectionFeedback feedback)
        => _restoredFeedback.Add(FeedbackIdentity(filePath, feedback));

    private void RestoreApprovedFeedback(string filePath, IEnumerable<Models.SectionFeedback> comments)
    {
        var key = CurrentFeedbackKey();
        var approved = comments.Where(c => c.ReviewStatus == Models.FeedbackReviewStatus.Approved).ToArray();
        if (approved.Length == 0 || listBoxStudents.SelectedItem is not Models.Student) return;
        foreach (var feedback in approved)
        {
            var identity = FeedbackIdentity(filePath, feedback);
            if (!_restoredFeedback.Add(identity)) continue;
            var text = Services.SavedFeedbackText.Format(feedback);
            var previous = _reviewDrafts.GetValueOrDefault(key, "");
            _reviewDrafts[key] = string.IsNullOrWhiteSpace(previous) ? text : previous + "\n\n---\n\n" + text;
        }
        WorkspaceFeedback_Click(this, new RoutedEventArgs());
    }
    private TextBox? _feedbackEditor;
    private string? _feedbackEditorKey;
    private string CurrentFeedbackKey() => System.Text.Json.JsonSerializer.Serialize(new[]
    {
        (listBoxStudents.SelectedItem as Models.Student)?.Folder,
        _currentAssignment?.Course, _currentAssignment?.Title
    });

    private void PresentGeneratedFeedback(string key, string text)
    {
        var previous = _reviewDrafts.GetValueOrDefault(key, "");
        _reviewDrafts[key] = string.IsNullOrWhiteSpace(previous) ? text : previous + "\n\n---\n\n" + text;
        if (key != CurrentFeedbackKey()) return;
        if (_feedbackEditor != null && _feedbackEditorKey == key)
        {
            _feedbackEditor.Text = _reviewDrafts[key];
            if (sidePanelContent.Child != null) OpenSidePanel();
        }
        else WorkspaceFeedback_Click(this, new RoutedEventArgs());
    }

    private void WorkspaceAssignment_Click(object sender, RoutedEventArgs e)
    {
        SetupAssignmentMenuItem_Click(sender, e);
        WorkspaceRubric_Click(sender, e);
    }

    private void WorkspaceRubric_Click(object sender, RoutedEventArgs e)
    {
        _feedbackEditor = null;
        _feedbackEditorKey = null;
        var content = new StackPanel { Margin = new Thickness(18) };
        void Text(string value, double size = 14) => content.Children.Add(new TextBlock
        {
            Text = value, FontSize = size, Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
        });
        Text(_currentAssignment?.Title ?? "Assignment and rubric", 20);
        Text(_currentAssignment?.Requirements ?? "Choose a saved assignment above or set up an assignment to define expectations and rubric criteria.");
        if (_currentAssignment != null)
        {
            Text("Rubric", 18);
            foreach (var criterion in _currentAssignment.Rubric)
                Text($"{criterion.Name} · {criterion.MaxPoints} points");
            if (_currentAssignment.Rubric.Count == 0) Text("No rubric criteria have been added yet.");
        }
        var edit = new Button { Content = "Set up assignment", Padding = new Thickness(10), Margin = new Thickness(0, 8, 0, 8) };
        edit.Click += WorkspaceAssignment_Click;
        content.Children.Add(edit);
        var close = new Button { Content = "Close panel", Padding = new Thickness(10) };
        close.Click += (_, _) => CloseSidePanel();
        content.Children.Add(close);
        sidePanelContent.Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        OpenSidePanel();
    }

    private void WorkspaceFeedback_Click(object sender, RoutedEventArgs e)
    {
        if (listBoxStudents.SelectedItem is not Models.Student student)
        {
            System.Windows.MessageBox.Show("Select an assignment and a student before preparing feedback.", "Feedback");
            return;
        }
        var assignmentTitle = _currentAssignment?.Title ?? "Submission feedback";
        var draftKey = CurrentFeedbackKey();
        var panel = new DockPanel();
        var close = new Button { Content = "Close feedback", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        close.Click += (_, _) => CloseSidePanel();
        DockPanel.SetDock(close, Dock.Top);
        panel.Children.Add(close);
        var heading = new TextBlock { Text = $"{student.FullName}\n{assignmentTitle}\nFeedback draft · export to keep a copy", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White, Margin = new Thickness(12) };
        DockPanel.SetDock(heading, Dock.Top);
        panel.Children.Add(heading);
        var draft = new TextBox { Text = _reviewDrafts.GetValueOrDefault(draftKey, ""), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 160, Margin = new Thickness(12), Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(40, 43, 48)) };
        _feedbackEditor = draft;
        _feedbackEditorKey = draftKey;
        var format = new ComboBox { ItemsSource = new[] { "TXT", "Markdown", "HTML" }, SelectedIndex = 0, Margin = new Thickness(12, 4, 12, 4), ToolTip = "Clipboard format" };
        DockPanel.SetDock(format, Dock.Bottom);
        panel.Children.Add(format);
        var copy = new Button { Content = "Copy feedback", Margin = new Thickness(12, 4, 12, 4), Padding = new Thickness(8), IsEnabled = !string.IsNullOrWhiteSpace(draft.Text) };
        draft.TextChanged += (_, _) =>
        {
            _reviewDrafts[draftKey] = draft.Text;
            copy.Content = "Copy feedback";
            copy.IsEnabled = !string.IsNullOrWhiteSpace(draft.Text);
        };
        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(Services.FeedbackCopyFormatter.Format(draft.Text, format.SelectedItem as string ?? "TXT"));
                copy.Content = "Copied!";
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                System.Windows.MessageBox.Show("The clipboard is busy. Please try copying again.", "Copy feedback");
            }
        };
        DockPanel.SetDock(copy, Dock.Bottom);
        panel.Children.Add(copy);
        var export = new Button { Content = "Export feedback…", Margin = new Thickness(12), Padding = new Thickness(8) };
        export.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Text document|*.txt", FileName = "feedback.txt" };
            if (dialog.ShowDialog(this) == true)
            {
                try { System.IO.File.WriteAllText(dialog.FileName, heading.Text.Split('\n')[0] + "\n" + assignmentTitle + "\n\n" + draft.Text); }
                catch (Exception ex) { System.Windows.MessageBox.Show("Could not export feedback: " + ex.Message, "Export feedback"); }
            }
        };
        DockPanel.SetDock(export, Dock.Bottom);
        panel.Children.Add(export);
        // Detach before reusing the existing view in a new panel.
        if (_gradingView.Parent is Panel parent) parent.Children.Remove(_gradingView);
        if (_gradingView.Parent is Expander oldExpander) oldExpander.Content = null;
        var programming = new Expander { Header = "Programming results and deductions", Content = _gradingView, IsExpanded = false, Margin = new Thickness(12), MaxHeight = 350 };
        DockPanel.SetDock(programming, Dock.Bottom);
        panel.Children.Add(programming);
        panel.Children.Add(draft);
        sidePanelContent.Child = panel;
        OpenSidePanel();
    }
}
