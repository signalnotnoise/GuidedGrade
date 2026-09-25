using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lab_Feedback_WPF.Controls;
using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.Windows;
using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UI_Framework.Wpf;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
[DoNotParallelize]
public class FrameworkMigrationTests
{
    private static void ClickShell(Lab_Feedback_WPF.MainWindow window, string name)
    {
        var host = (FrameworkElement)window.Content;
        Layout(host, 1440, 960);
        Descendants<Button>(host).Single(button => AutomationProperties.GetName(button) == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    [TestMethod]
    public void FileTabsCloseIndependentlyAndToolsKeepTheirNativeDocument() => RunSta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "ShellTabs-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "test.db");
        var window = new Lab_Feedback_WPF.MainWindow(new AssignmentPersistenceService(database), new CommentPersistenceService(database));
        var host = (ViewHost)window.Content;
        try
        {
            var tree = window.fileTreeView;
            foreach (var name in new[] { "One.cs", "Two.cs" })
            {
                var path = Path.Combine(directory, name); File.WriteAllText(path, "// " + name);
                var item = new FileSystemItem(path, false); tree.Items.Add(item);
                Layout(host, 1200, 800);
                ((TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(item)).IsSelected = true;
            }
            Assert.AreEqual("// Two.cs", window.codeEditor.Text);
            ClickShell(window, "Close One.cs");
            Assert.AreEqual("// Two.cs", window.codeEditor.Text, "Closing an inactive tab must not select it.");
            var document = window.runtimeTerminalRichTextBox.Document;
            window.runtimeTerminalRichTextBox.AppendText("Terminal state survives panel switches.");
            ClickShell(window, "Console tab");
            ClickShell(window, "Violations tab");
            ClickShell(window, "Console tab");
            Assert.AreSame(document, window.runtimeTerminalRichTextBox.Document);
            StringAssert.Contains(new System.Windows.Documents.TextRange(document.ContentStart, document.ContentEnd).Text, "Terminal state survives");
            ClickShell(window, "Close Two.cs");
            Assert.AreEqual("", window.codeEditor.Text);
            Layout(host, 1000, 600);
            Assert.IsTrue(Descendants<TextBlock>(host).Any(label => label.Text == "Review student work" && label.ActualWidth > 0));
            window.Close();
            Assert.ThrowsException<ObjectDisposedException>(() => host.Refresh());
        }
        finally
        {
            window.Close(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    });

    [TestMethod]
    public void OverallReviewCompletionFollowsOriginalFileAndClearInvalidatesPendingResult() => RunSta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "ReviewCompletion-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "Program.cs");
        var other = Path.Combine(directory, "Other.cs");
        File.WriteAllText(source, "// Original source must remain unchanged.\n");
        File.WriteAllText(other, "// A different open file.\n");
        var database = Path.Combine(directory, "reviews.db");
        var persistence = new CommentPersistenceService(database);
        persistence.SaveComments(source, new[] { new SectionFeedback { SectionName = "Existing section", StartLine = 1, EndLine = 1 } });
        var window = new Lab_Feedback_WPF.MainWindow(new AssignmentPersistenceService(database), persistence);
        try
        {
            var host = (FrameworkElement)window.Content;
            var tree = window.fileTreeView;
            var fileItem = new FileSystemItem(source, false);
            var otherItem = new FileSystemItem(other, false);
            tree.Items.Add(fileItem); tree.Items.Add(otherItem);
            Layout(host, 1200, 800);
            ((TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(otherItem)).IsSelected = true;
            var pending = window.CaptureOverallReviewFiles(new[] { source });
            window.CompleteOverallFileReview(pending, "original-student-draft", "Overall feedback from the completed job.");
            var reloaded = new CommentPersistenceService(database).LoadComments(source);
            Assert.AreEqual(2, reloaded.Count, "Existing unopened section comments must survive.");
            Assert.AreEqual("Overall feedback from the completed job.", reloaded.Single(item => item.IsOverallReview).Explanation);
            Assert.AreEqual(0, persistence.LoadComments(other).Count);
            var editor = window.codeEditor;
            Assert.AreEqual(File.ReadAllText(other), editor.Text, "Finishing a job must not switch or overwrite the selected file.");
            ((TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(fileItem)).IsSelected = true;
            Layout(host, 1200, 800);
            Assert.IsTrue(Descendants<InlineCommentAdorner>(host).Any(card => card.Feedback.IsOverallReview));
            window.CompleteOverallFileReview(window.CaptureOverallReviewFiles(new[] { source }), "original-student-draft", "Updated overall review.");
            Assert.AreEqual(1, persistence.LoadComments(source).Count(item => item.IsOverallReview));
            var inFlight = window.CaptureOverallReviewFiles(new[] { source });
            tree.ContextMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Clear review"))
                .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            window.CompleteOverallFileReview(inFlight, "original-student-draft", "Must not return after Clear review.");
            Assert.AreEqual(0, new CommentPersistenceService(database).LoadComments(source).Count);
            Assert.IsFalse(Descendants<InlineCommentAdorner>(host).Any());
            Assert.AreEqual("// Original source must remain unchanged.\n", File.ReadAllText(source));
        }
        finally
        {
            window.Close();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var path in Directory.GetFiles(directory)) File.Delete(path);
            Directory.Delete(directory);
        }
    });

    [TestMethod]
    public void SampleDirectoryRendersRealFilesAndMenus() => RunSta(() =>
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Lab Feedback WPF.slnx"))) root = root.Parent;
        Assert.IsNotNull(root);
        var sampleRoot = Path.Combine(root.FullName, "Demo", "Submissions");
        var students = Student.GetStudentsFromFolders(sampleRoot);
        Assert.AreEqual(2, students.Count);
        var database = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        var persistence = new CommentPersistenceService(database);
        var source = Path.Combine(sampleRoot, "Example_Alex-DEMO001", "Lab_04_Conditions", "Program.cs");
        persistence.SaveComments(source, new[] { new SectionFeedback
        {
            SectionName = "DEMO: range validation", StartLine = 10, EndLine = 11,
            Strengths = new() { "Parses whole-number input safely." },
            Issues = new() { "Scores below 0 or above 100 still receive a result." },
            SuggestedCode = "if (score < 0 || score > 100) return;",
            Explanation = "Seeded demo feedback, not an AI-generated review.", SuggestedScore = 1
        } });
        var window = new Lab_Feedback_WPF.MainWindow(new AssignmentPersistenceService(database), persistence);
        try
        {
            var studentList = window.listBoxStudents;
            foreach (var student in students) studentList.Items.Add(student);
            studentList.SelectedItem = students.Single(student => student.IdNumber == "DEMO001");
            var host = (FrameworkElement)window.Content;
            Layout(host, 1440, 960);
            var tree = window.fileTreeView;
            var folder = ((FileSystemItem)tree.Items[0]).Children.Single(item => item.Name == "Lab_04_Conditions");
            var rootContainer = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0);
            rootContainer.IsExpanded = true;
            Layout(host, 1440, 960);
            var folderContainer = (TreeViewItem)rootContainer.ItemContainerGenerator.ContainerFromItem(folder);
            folderContainer.IsExpanded = true;
            Layout(host, 1440, 960);
            var file = folder.Children.Single(item => item.Name == "Program.cs");
            ((TreeViewItem)folderContainer.ItemContainerGenerator.ContainerFromItem(file)).IsSelected = true;
            file.IsCheckedForAnalysis = true;
            Layout(host, 1440, 960);
            Assert.AreSame(file, tree.SelectedItem);
            var editor = window.codeEditor;
            Assert.AreEqual(File.ReadAllText(source), editor.Text);
            var tabs = host;
            Assert.IsTrue(Descendants<TextBlock>(tabs).Any(label => label.Text == "Program.cs" && label.ActualWidth > 0));
            Assert.IsTrue(Descendants<Button>(tabs).Any(button => AutomationProperties.GetName(button) == "Close Program.cs" && button.ActualWidth > 0));
            var close = Descendants<Button>(tabs).Single(button => AutomationProperties.GetName(button) == "Close Program.cs");
            Assert.IsTrue(Descendants<TextBlock>(close).Any(label => label.Text == "×" && label.ActualWidth > 0 && label.ActualHeight > 0));
            Capture(host, "demo-files", 1440, 960);
            ClickShell(window, "Comments panel");
            Layout(host, 1440, 960);
            var draft = Descendants<TextBox>(host).Single(control => AutomationProperties.GetName(control) == "Editable feedback draft");
            draft.Text = "DEMO FEEDBACK — manually seeded for this visual check.\n\n"
                + "Input parsing: the submission uses TryParse and explains invalid text input clearly.\n\n"
                + "Range validation: values below 0 or above 100 still produce a classification. Add a range check before computing the result.\n\n"
                + "Selection logic: the passing threshold of 70 is expressed clearly. Test the boundary values 69 and 70, plus 0 and 100.\n\n"
                + "Next steps: test negative values, values above 100, and nonnumeric input. Keep each error message specific so the person using the program knows how to correct it.\n\n"
                + "This is fictitious demonstration feedback, not a real student assessment or an AI result.";
            Flush();
            Capture(host, "demo-feedback", 1440, 960);
            var review = Descendants<InlineCommentAdorner>(host).Single();
            Assert.IsFalse(review.IsExpanded);
            review.IsExpanded = true;
            Capture(host, "demo-expanded-review", 1440, 960);
            Assert.IsTrue(Descendants<Button>(review).Any(button => Equals(button.Content, "Approve")));
            review.IsExpanded = false;
            var menu = tree.ContextMenu;
            menu.PlacementTarget = tree;
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            Capture(menu, "demo-file-menu", 440, 300);
            Assert.AreEqual(Visibility.Visible, menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Clear review")).Visibility);
            var solution = folder.Children.Single(item => item.IsSolution);
            ((TreeViewItem)folderContainer.ItemContainerGenerator.ContainerFromItem(solution)).IsSelected = true;
            Layout(host, 1440, 960);
            Assert.AreSame(solution, tree.SelectedItem);
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            Capture(menu, "demo-solution-menu", 440, 330);
            var programming = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Programming tools"));
            Assert.AreEqual(Visibility.Visible, programming.Visibility);
            CollectionAssert.IsSubsetOf(new[] { "Build", "Build and Run", "Run" }, programming.Items.OfType<MenuItem>().Select(item => (string)item.Header).ToArray());
            menu.ApplyTemplate(); programming.ApplyTemplate();
            programming.IsSubmenuOpen = true;
            Flush();
            if (programming.Template.FindName("PART_Popup", programming) is System.Windows.Controls.Primitives.Popup popup && popup.Child is FrameworkElement submenu)
                Capture(submenu, "demo-build-menu", 310, 230);
            programming.IsSubmenuOpen = false;
        }
        finally
        {
            window.Close();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(database);
        }
    });

    [TestMethod]
    public void DeclarativeFormsRenderRetainInputAndDisposeTheirHosts() => RunSta(() =>
    {
        var window = new SettingsWindow();
        var host = (ViewHost)window.Content;
        Layout(host, 700, 780);
        var input = Descendants<TextBox>(host).Single(control => AutomationProperties.GetName(control) == "New violation");
        input.Text = "Migration test term";
        Flush();
        Assert.AreSame(input, Descendants<TextBox>(host).Single(control => AutomationProperties.GetName(control) == "New violation"));
        Descendants<Button>(host).First(button => Equals(button.Content, "Add")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Flush();
        CollectionAssert.Contains(window.Violations, "Migration test term");
        Assert.AreEqual("", input.Text);
        Capture(host, "settings", 700, 780);
        window.Close();
        Assert.ThrowsException<ObjectDisposedException>(() => host.Refresh());

        var provider = new LLMSettingsWindow();
        var providerHost = (ViewHost)provider.Content;
        Layout(providerHost, 700, 780);
        var providerPicker = Descendants<ComboBox>(providerHost).Single(control => AutomationProperties.GetName(control) == "AI provider");
        providerPicker.SelectedIndex = 1;
        Flush(); Layout(providerHost, 700, 780);
        Assert.IsTrue(Descendants<PasswordBox>(providerHost).Any(control => AutomationProperties.GetName(control) == "Azure API key"));
        Capture(providerHost, "provider", 700, 780);
        provider.Close();

        var section = new SectionGradingDialog(new() { new("Correctness", 8), new("Validation", 2) });
        Capture((FrameworkElement)section.Content, "section", 560, 480);
        section.Close();

        var database = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        var assignment = new AssignmentSetupWindow(new AssignmentPersistenceService(database), new GradingAssignment
        {
            Course = "Programming", Title = "Input validation", Requirements = "Accept valid input and explain invalid input.",
            Rubric = new() { new("Correctness", 7.5), new("Validation", 2.5) }
        });
        Capture((FrameworkElement)assignment.Content, "assignment", 780, 800);
        assignment.Close();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(database);
    });

    [TestMethod]
    public void WorkspaceKeepsNativeEditorMenusAndFrameworkPanels() => RunSta(() =>
    {
        var database = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        var window = new Lab_Feedback_WPF.MainWindow(new AssignmentPersistenceService(database), new CommentPersistenceService(database));
        try
        {
            var workspaceHost = (ViewHost)window.Content;
            var editor = window.codeEditor;
            workspaceHost.Refresh();
            Assert.AreSame(editor, window.codeEditor);
            Capture((FrameworkElement)window.Content, "workspace", 1200, 800);
            var tree = window.fileTreeView;
            Assert.IsTrue(tree.ContextMenu.Items.OfType<MenuItem>().Any(item => Equals(item.Header, "Clear review")));
            Assert.IsTrue(tree.ContextMenu.Items.OfType<MenuItem>().Any(item => Equals(item.Header, "Overall feedback for checked files")));
            var queueTab = window.queueDetailsTab;
            Assert.IsInstanceOfType<ViewHost>(queueTab.Content);
            Capture((FrameworkElement)queueTab.Content, "queue", 460, 740);
            Assert.IsNotNull(window.codeEditor);
            Assert.IsNotNull(window.runtimeTerminalRichTextBox);
            var students = window.listBoxStudents;
            students.Items.Add(new Student("Demo", "Student", "", null));
            students.SelectedIndex = 0;
            ClickShell(window, "Comments panel");
            var comments = window.commentsDetailsTab;
            var feedbackPanel = (FrameworkElement)comments.Content;
            Layout(feedbackPanel, 500, 740);
            var draft = Descendants<TextBox>(feedbackPanel).Single(control => AutomationProperties.GetName(control) == "Editable feedback draft");
            draft.Text = "Instructor edits must remain when changing panels.";
            Flush();
            ClickShell(window, "Rubric panel");
            ClickShell(window, "Comments panel");
            Assert.AreSame(feedbackPanel, comments.Content);
            Assert.AreEqual("Instructor edits must remain when changing panels.", draft.Text);
            Capture(feedbackPanel, "feedback", 500, 740);
        }
        finally
        {
            window.Close();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(database);
        }
    });

    [TestMethod]
    public void ExtractionKeepsNativeProgressAndCancelsWithoutInvalidatingWorkerTokens() => RunSta(() =>
    {
        var dialog = new Lab_Feedback_WPF.Views.ExtractionProgressDialog();
        using var first = dialog.AddOperation("first", "First archive", 5);
        using var second = dialog.AddOperation("second", "Second archive", 3);
        var host = (ViewHost)dialog.Content;
        Layout(host, 500, 520);
        var bar = Descendants<ProgressBar>(host).First();
        dialog.UpdateOperation("first", 2, "Extracting file...");
        Flush();
        Assert.AreSame(bar, Descendants<ProgressBar>(host).First());
        Assert.AreEqual(2d, bar.Value);
        Descendants<Button>(host).First(button => Equals(button.Content, "Cancel"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Flush();
        Assert.IsTrue(first.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(second.CancellationToken.IsCancellationRequested);
        dialog.UpdateOperation("first", 3, "Late update");
        Assert.AreEqual("Cancelled", first.CurrentFile);
        Capture(host, "extraction", 500, 520);
        Descendants<Button>(host).Single(button => Equals(button.Content, "Cancel all"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.IsTrue(second.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(dialog.GlobalCancellationToken.IsCancellationRequested);
        first.Dispose(); second.Dispose();
        Assert.IsTrue(first.CancellationToken.IsCancellationRequested);
        dialog.FailOperation("second", "Late failure");
        Assert.AreEqual("Cancelled", second.CurrentFile);
        Assert.ThrowsException<ObjectDisposedException>(() => host.Refresh());
    });

    [TestMethod]
    public void InlineReviewRetainsEventsAndReleasesFrameworkHost() => RunSta(() =>
    {
        var feedback = new SectionFeedback
        {
            SectionName = "Validate input", SuggestedScore = 2.5,
            Strengths = new() { "Accepts valid input." }, Issues = new() { "Reject negative values." },
            SuggestedCode = "if (value < 0) return;", Explanation = "Check before using the value."
        };
        using var card = new InlineCommentAdorner(feedback, 5);
        Assert.IsFalse(card.IsExpanded, "Reviews should not cover source code until opened.");
        card.IsExpanded = true;
        var host = (ViewHost)card.Content;
        card.Measure(new Size(600, double.PositiveInfinity));
        Assert.IsTrue(card.DesiredSize.Height <= 400, "Long reviews must scroll within the editor overlay, not clip their actions.");
        Layout(card, 600, 650);
        var approvals = 0;
        card.ApproveRequested += (_, value) => { Assert.AreSame(feedback, value); approvals++; card.MarkApproved(); };
        Descendants<Button>(card).Single(button => Equals(button.Content, "Approve")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Flush();
        Assert.AreEqual(1, approvals);
        Assert.IsFalse(Descendants<Button>(card).Any(button => Equals(button.Content, "Approve")));
        card.IsExpanded = false; Flush();
        Assert.IsFalse(Descendants<TextBox>(card).Any());
        card.IsExpanded = true; Flush();
        Capture(card, "review-card", 600, 650);
        card.Dispose();
        Assert.ThrowsException<ObjectDisposedException>(() => host.Refresh());
    });

    internal static void Capture(FrameworkElement element, string name, int width, int height)
    {
        Layout(element, width, height);
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Lab Feedback WPF.slnx"))) root = root.Parent;
        Assert.IsNotNull(root);
        var directory = Path.Combine(root.FullName, "artifacts", "ui-migration");
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(output);
    }

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout(); Flush();
    }
    private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<T> Descendants<T>(DependencyObject element) where T : DependencyObject
    {
        if (element is T found) yield return found;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(element, i))) yield return child;
    }
    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "UI migration check timed out.");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
