using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GuidedGrade.Controls;
using GuidedGrade.Models;
using GuidedGrade.Windows;
using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UI_Framework.Wpf;

namespace GuidedGrade.Tests.Services;

[TestClass]
[DoNotParallelize]
public class FrameworkMigrationTests
{
    [TestMethod]
    public void LogPanelRendersDecodedSnapshotAndClearsPreviousContent() => RunSta(() =>
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(0x474C5346u); writer.Write(2f); writer.Write(1u);
                var name = System.Text.Encoding.ASCII.GetBytes("Game.cpp"); writer.Write((uint)name.Length); writer.Write(name);
                writer.Write(1700000000ul);
                var content = System.Text.Encoding.ASCII.GetBytes("void Run() {}").Select(b => unchecked((byte)(b + 128))).ToArray(); writer.Write((uint)content.Length); writer.Write(content);
            }
            var model = new GuidedGrade.ViewModels.LogPanelViewModel();
            using var host = GuidedGrade.Presentation.ReviewTheme.Host(() => new GuidedGrade.Views.LogPanelView(model).Build());
            Layout(host, 1000, 300);
            model.Load(path); Flush(); Layout(host, 1000, 300);
            Assert.AreEqual("void Run() {}", model.Source.Text);
            Assert.AreEqual(1, model.Snapshots.Items.Count);
            Assert.AreEqual(1, model.BuildCount);
            Assert.IsTrue(model.IsLoaded.Value);
            Assert.AreEqual(1, model.RecordedBuildCount.Value);
            Assert.AreEqual(1, model.History.Points.Count);
            Assert.AreNotEqual(model.Snapshots.Foreground.ToString(), model.Snapshots.Background.ToString());
            Assert.AreNotEqual(model.Source.Foreground.ToString(), model.Source.Background.ToString());
            model.Clear("No log for current student"); Flush(); Layout(host, 1000, 300);
            Assert.AreEqual("", model.Source.Text);
            Assert.AreEqual(0, model.Snapshots.Items.Count);
            Assert.IsFalse(model.IsLoaded.Value);
            Assert.AreEqual(0, model.RecordedBuildCount.Value);
        }
        finally { File.Delete(path); }
    });

    [TestMethod]
    public void WindowInputDispatcherDeliversPhysicalKeysToOwnedWindow() => RunSta(() =>
    {
        var text = new TextBox();
        var window = new Window { Title = "GuidedGrade input validation", Width = 320, Height = 180, Content = text };
        var keys = new List<System.Windows.Input.Key>();
        var clicks = 0;
        window.AddHandler(System.Windows.Input.Mouse.MouseUpEvent, new System.Windows.Input.MouseButtonEventHandler((_, _) => clicks++), true);
        text.KeyUp += (_, e) => keys.Add(e.Key);
        try
        {
            window.Show(); window.Activate(); text.Focus(); Flush();
            foreach (var key in new ushort[] { 0x57, 0x20, 0x1B })
            {
                WindowInputDispatcher.Send((uint)System.Diagnostics.Process.GetCurrentProcess().Id, new("key", key));
                var frame = new DispatcherFrame();
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
                timer.Start(); Dispatcher.PushFrame(frame);
            }
            CollectionAssert.AreEqual(new[] { System.Windows.Input.Key.W, System.Windows.Input.Key.Space, System.Windows.Input.Key.Escape }, keys);
            Assert.AreEqual("w ", text.Text.ToLowerInvariant());
            WindowInputDispatcher.Send((uint)System.Diagnostics.Process.GetCurrentProcess().Id, new("click", X: 0.5, Y: 0.5));
            var clickFrame = new DispatcherFrame();
            var clickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            clickTimer.Tick += (_, _) => { clickTimer.Stop(); clickFrame.Continue = false; };
            clickTimer.Start(); Dispatcher.PushFrame(clickFrame);
            Assert.AreEqual(1, clicks);
        }
        finally { window.Close(); }
    });

    [TestMethod]
    public void EarlierSectionsRemainVisibleAndReopeningReadsFreshPersistedComments() => RunSta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "EarlierReviews-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "Code.cpp"); File.WriteAllText(file, "// line one\n// line two");
        var database = Path.Combine(directory, "reviews.db");
        var persistence = new CommentPersistenceService(database);
        var assignment = new GradingAssignment { Course = "PG2", Title = "Lab 1" };
        persistence.SaveComments(file, [new() { SectionName = "Old section", StartLine = 1, EndLine = 1,
            Explanation = "Earlier saved explanation", ReviewStatus = FeedbackReviewStatus.Approved }]);
        var window = new GuidedGrade.MainWindow(new AssignmentPersistenceService(database), persistence);
        try
        {
            window.SetReviewAssignment(assignment);
            window.listBoxStudents.Items.Add(new Student("Alex", "Rivera", "1", directory));
            window.listBoxStudents.SelectedIndex = 0;
            var host = (FrameworkElement)window.Content;
            Layout(host, 1200, 800);
            Assert.AreEqual(1, Descendants<InlineCommentAdorner>(host).Count());
            ClickShell(window, "Comments panel");
            var panel = (FrameworkElement)window.commentsDetailsTab.Content;
            Layout(panel, 400, 600);
            Assert.IsTrue(Descendants<TextBlock>(panel).Any(t => t.Text.Contains("Earlier saved explanation")));
            var draft = Descendants<TextBox>(panel).Single(t => AutomationProperties.GetName(t) == "Editable feedback draft");
            Assert.IsFalse(draft.Text.Contains("Earlier saved explanation"));
            var saved = persistence.LoadComments(file);
            saved.Add(new() { SectionName = "New section", StartLine = 2, EndLine = 2, Explanation = "Fresh saved section",
                ReviewContext = ReviewContext.Key(directory, assignment, file) });
            persistence.SaveComments(file, saved);
            Assert.IsTrue(window.OpenSavedReviewForSelectedStudent());
            Layout(host, 1200, 800);
            Assert.AreEqual(2, Descendants<InlineCommentAdorner>(host).Count());
            window.SetReviewAssignment(new() { Course = "PG2", Title = "Different" });
            Layout(host, 1200, 800);
            Assert.AreEqual(1, Descendants<InlineCommentAdorner>(host).Count());
            Assert.AreEqual("Old section", Descendants<InlineCommentAdorner>(host).Single().Feedback.SectionName);
        }
        finally { window.Close(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    });

    [TestMethod]
    public void ReviewMenuUsesSelectionAndClearsOnlyCurrentAssignment() => RunSta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "ReviewMenu-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "reviews.db");
        var comments = new CommentPersistenceService(database);
        var window = new GuidedGrade.MainWindow(new AssignmentPersistenceService(database), comments);
        try
        {
            var host = (FrameworkElement)window.Content;
            Layout(host, 1200, 800);
            Assert.IsFalse(Descendants<MenuItem>(host).Any(m => AutomationProperties.GetName(m) == "Review menu"));
            var assignment = new GradingAssignment { Course = "PG2", Title = "Lab 1", Rubric = [new("Correctness", 100)] };
            window.SetReviewAssignment(assignment);
            var file = Path.Combine(directory, "One.cpp"); var other = Path.Combine(directory, "Two.cpp");
            File.WriteAllText(file, "int main() {} "); File.WriteAllText(other, "// other");
            var item = new FileSystemItem(file, false);
            window.fileTreeView.Items.Add(item);
            Layout(host, 1200, 800);
            var review = Descendants<MenuItem>(host).Single(m => AutomationProperties.GetName(m) == "Review menu");
            void Refresh() => review.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent, review));
            MenuItem Option(string label) => review.Items.OfType<MenuItem>().Single(m => Equals(m.Header, label));
            Refresh();
            Assert.IsFalse(Option("Selection review").IsEnabled);
            Assert.IsFalse(Option("Overall review").IsEnabled);
            Assert.IsFalse(Option("Clear reviews").IsEnabled);
            item.IsCheckedForAnalysis = true;
            ((TreeViewItem)window.fileTreeView.ItemContainerGenerator.ContainerFromItem(item)).IsSelected = true;
            window.codeEditor.Select(0, 3);
            Refresh();
            Assert.IsTrue(Option("Selection review").IsEnabled);
            Assert.IsTrue(Option("Overall review").IsEnabled);
            Assert.IsTrue(Option("Clear reviews").IsEnabled);
            SectionFeedback Feedback(string path, GradingAssignment a, string name) => new() {
                SectionName = name, ReviewContext = ReviewContext.Key(directory, a, path), ReviewStatus = FeedbackReviewStatus.Rejected };
            var different = new GradingAssignment { Course = "PG2", Title = "Lab 2" };
            comments.SaveComments(file, [Feedback(file, assignment, "Delete"), Feedback(file, different, "Keep"), new() { SectionName = "Legacy" }]);
            comments.SaveComments(other, [Feedback(other, assignment, "Other file")]);
            window.ClearAssignmentReviews(false);
            CollectionAssert.AreEquivalent(new[] { "Keep", "Legacy" }, comments.LoadComments(file).Select(c => c.SectionName).ToArray());
            Assert.AreEqual(1, comments.LoadComments(other).Count);
            window.ClearAssignmentReviews(true);
            Assert.AreEqual(0, comments.LoadComments(other).Count);
            Assert.AreEqual(2, comments.LoadComments(file).Count);
            Capture(host, "workspace-review-menu", 1200, 800);
        }
        finally { window.Close(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    });

    [TestMethod]
    public void DatabaseWritesRefreshPersistedGradesAndAssignmentSelectors() => RunSta(() =>
    {
        var database = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        var persistence = new AssignmentPersistenceService(database);
        var window = new GuidedGrade.MainWindow(persistence, new CommentPersistenceService(database));
        try
        {
            var folder = Path.Combine(Path.GetTempPath(), "RefreshStudent");
            var student = new Student("Alex", "Rivera", "1", folder);
            var first = new GradingAssignment { Course = "PG2", Title = "Lab 1", Rubric = [new("Correctness", 100)] };
            var second = new GradingAssignment { Course = "PG2", Title = "Lab 2", Rubric = [new("Correctness", 100)] };
            var firstKey = ReviewContext.Key(folder, first, Path.Combine(folder, "Lab", "One.cpp"));
            var secondKey = ReviewContext.Key(folder, second, Path.Combine(folder, "Lab", "Two.cpp"));
            // Another persistence owner writes after the window took its initial snapshot.
            new GradePersistenceService(database).Save(secondKey, new(60, 100));
            window.SetReviewAssignment(first);
            window.SaveStudentGrade(firstKey, new(80, 100));
            Assert.AreEqual(new StudentGrade(140, 200), window.TotalForStudent(student));
            window.SaveStudentGrade(firstKey, null);
            Assert.AreEqual(new StudentGrade(60, 100), window.TotalForStudent(student));

            persistence.SaveAssignment(first);
            first.Requirements = "Persisted instructions";
            persistence.SaveAssignment(first);
            window.RefreshSavedAssignmentSelection(new() { Course = first.Course, Title = first.Title, Requirements = "Stale caller" });
            var host = (FrameworkElement)window.Content;
            Layout(host, 1200, 800);
            var course = Descendants<MenuItem>(host).Single(c => AutomationProperties.GetName(c) == "Saved course");
            var assignment = Descendants<MenuItem>(host).Single(c => AutomationProperties.GetName(c) == "Saved assignment");
            Assert.AreEqual("Course: PG2", course.Header);
            Assert.IsTrue(course.Items.OfType<MenuItem>().Single().IsChecked);
            Assert.AreEqual("Assignment: Lab 1", assignment.Header);
            Assert.IsTrue(assignment.Items.OfType<MenuItem>().Single().IsChecked);
            ClickShell(window, "Rubric panel");
            var panel = (FrameworkElement)window.rubricDetailsTab.Content;
            Layout(panel, 400, 600);
            Descendants<CheckBox>(panel).Single(c => Equals(c.Content, "Show assignment instructions")).IsChecked = true;
            Layout(panel, 400, 600);
            Assert.IsTrue(Descendants<TextBlock>(panel).Any(t => t.Text == "Persisted instructions"));
        }
        finally { window.Close(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(database); }
    });

    [TestMethod]
    public void SelectingStudentReopensPersistedBatchReviewInNestedFolderAndBothDisplays() => RunSta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "BatchDisplay-" + Guid.NewGuid());
        var student = new Student("Alex", "Rivera", "1", Path.Combine(directory, "StudentA"));
        var file = Path.Combine(student.Folder!, "Extra", "Lab_2_Conversions", "Lab 2", "StudentWork.h");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, string.Join("\n", Enumerable.Range(1, 200).Select(i => "// source " + i)));
        var database = Path.Combine(directory, "reviews.db");
        var assignment = new GradingAssignment { Course = "PG1", Title = "Conversions" };
        var persistence = new CommentPersistenceService(database);
        persistence.SaveComments(file, [new SectionFeedback { IsOverallReview = true, SectionName = "Overall file review",
            StartLine = 1, EndLine = 1, Explanation = "Persisted approved batch feedback", ReviewStatus = FeedbackReviewStatus.Approved,
            ReviewContext = ReviewContext.Key(student.Folder, assignment, file) }]);
        var window = new GuidedGrade.MainWindow(new AssignmentPersistenceService(database), new CommentPersistenceService(database));
        try
        {
            var host = (FrameworkElement)window.Content;
            window.SetReviewAssignment(assignment);
            window.listBoxStudents.Items.Add(student);
            window.listBoxStudents.SelectedItem = student;
            Layout(host, 1200, 800);
            StringAssert.Contains(window.codeEditor.Text, "// source 1");
            Assert.IsFalse(Descendants<InlineCommentAdorner>(host).Any());
            var draft = Descendants<TextBox>((FrameworkElement)window.commentsDetailsTab.Content).Single(c => AutomationProperties.GetName(c) == "Editable feedback draft");
            StringAssert.Contains(draft.Text, "Persisted approved batch feedback");
            window.codeEditor.ScrollToLine(180);
            Layout(host, 1200, 800);
            Assert.IsFalse(Descendants<InlineCommentAdorner>(host).Any());
            window.SetReviewAssignment(new() { Course = "PG1", Title = "Different assignment" });
            Assert.IsFalse(window.OpenSavedReviewForSelectedStudent());
            Layout(host, 1200, 800);
            Assert.AreEqual(0, Descendants<InlineCommentAdorner>(host).Count());
        }
        finally { window.Close(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    });

    [TestMethod]
    public void BatchReplacementClearsOnlyTargetFileAndInvalidatesOldCompletions() => RunSta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "BatchReplace-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "reviews.db");
        var persistence = new CommentPersistenceService(database);
        var window = new GuidedGrade.MainWindow(new AssignmentPersistenceService(database), persistence);
        try
        {
            var file = Path.Combine(directory, "One.cs"); var other = Path.Combine(directory, "Two.cs");
            var context = ReviewContext.Key(directory, new() { Title = "Assignment" }, file);
            persistence.SaveComments(file, [new SectionFeedback { SectionName = "Old section", ReviewContext = context }, new SectionFeedback { SectionName = "Legacy review" }]);
            persistence.SaveComments(other, [new SectionFeedback { SectionName = "Keep me" }]);
            var stale = window.CaptureOverallReviewFiles([file]);
            var version = window.ClearBatchFileReview(file, context);
            Assert.AreEqual(0, persistence.LoadComments(file).Count);
            Assert.AreEqual(1, persistence.LoadComments(other).Count);
            window.CompleteOverallFileReview(stale, context, "Stale response");
            Assert.AreEqual(0, persistence.LoadComments(file).Count);
            window.CompleteOverallFileReview([(file, version)], context, "Replacement overall review", approve: true);
            var saved = persistence.LoadComments(file).Single();
            Assert.IsTrue(saved.IsOverallReview);
            Assert.AreEqual(FeedbackReviewStatus.Approved, saved.ReviewStatus);
            Assert.AreEqual("Replacement overall review", saved.Explanation);
        }
        finally { window.Close(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    });

    [TestMethod]
    public void BatchDesignerRequiresFreshPreviewForMultipleFiles() => RunSta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "BatchDesigner-" + Guid.NewGuid());
        var student = new Student("Alex", "Rivera", "1", Path.Combine(directory, "Rivera_Alex-1"));
        Directory.CreateDirectory(Path.Combine(student.Folder!, "Lab1"));
        foreach (var name in new[] { "Main.cs", "Helpers.cs" }) File.WriteAllText(Path.Combine(student.Folder!, "Lab1", name), "// code");
        var database = Path.Combine(directory, "test.db");
        var window = new GuidedGrade.MainWindow(new AssignmentPersistenceService(database), new CommentPersistenceService(database));
        var dialog = window.CreateBatchReviewDialog(new() { Course = "Programming", Title = "Lab 1", Rubric = [new("Correctness", 100)] }, [student]);
        try
        {
            var host = (FrameworkElement)dialog.Content;
            Layout(host, 650, 720);
            Button Queue() => Descendants<Button>(host).Single(b => Equals(b.Content, "Queue batch"));
            Assert.IsFalse(Queue().IsEnabled);
            var files = Descendants<TextBox>(host).Single(c => AutomationProperties.GetName(c) == "Batch review files");
            files.Text = "Lab1/Main.cs\nLab1/Helpers.cs";
            Descendants<Button>(host).Single(b => Equals(b.Content, "Preview students")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(host, 650, 720);
            Assert.IsTrue(Queue().IsEnabled);
            StringAssert.Contains(Descendants<TextBox>(host).Single(c => AutomationProperties.GetName(c) == "Batch preview").Text, "Ready · 2 files");
            Assert.IsTrue(Descendants<CheckBox>(host).Single(c => Equals(c.Content, "Automatically approve comments and add them to feedback")).IsChecked == true);
            Assert.IsFalse(Descendants<CheckBox>(host).Single(c => Equals(c.Content, "Build and run before reviewing")).IsChecked == true);
            Assert.IsFalse(Descendants<TextBox>(host).Single(c => AutomationProperties.GetName(c) == "Batch test entry point").IsEnabled);
            Capture(host, "batch-review", 650, 720);
            files.Text = "Lab1/Missing.cs";
            Layout(host, 650, 720);
            Assert.IsFalse(Queue().IsEnabled, "Changing paths must require a new preview.");
        }
        finally { dialog.Close(); window.Close(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    });

    [TestMethod]
    public void ApprovingSavedReviewTransfersImmediatelyAndKeepsExistingDraftWithoutDuplicates() => RunSta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "ReloadReviews-" + Guid.NewGuid());
        var student = new Student("Alex", "Rivera", "1", Path.Combine(directory, "Student"));
        var first = Path.Combine(student.Folder!, "Lab1", "First.cs");
        var second = Path.Combine(student.Folder!, "Lab1", "Second.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        File.WriteAllText(first, "// first"); File.WriteAllText(second, "// second");
        var database = Path.Combine(directory, "reviews.db");
        var assignment = new GradingAssignment { Course = "Course", Title = "Lab 1" };
        var context = ReviewContext.Key(student.Folder, assignment, first);
        var persistence = new CommentPersistenceService(database);
        persistence.SaveComments(first.ToUpperInvariant(), [new SectionFeedback
        {
            SectionName = "Pending review", StartLine = 1, EndLine = 1,
            Explanation = "Saved pending text", ReviewContext = ReviewContext.Key(student.Folder!.ToUpperInvariant(), assignment, first)
        }]);
        persistence.SaveComments(second, [new SectionFeedback
        {
            SectionName = "Approved review", StartLine = 1, EndLine = 1,
            Explanation = "Saved approved text", ReviewContext = context
        }, new SectionFeedback
        {
            SectionName = "Rejected review", StartLine = 1, EndLine = 1,
            Explanation = "Rejected text", ReviewContext = context, ReviewStatus = FeedbackReviewStatus.Rejected
        }]);
        var window = new GuidedGrade.MainWindow(new AssignmentPersistenceService(database), new CommentPersistenceService(database));
        try
        {
            var host = (FrameworkElement)window.Content;
            void Open(string path)
            {
                window.fileTreeView.Items.Clear();
                var item = new FileSystemItem(path, false); window.fileTreeView.Items.Add(item);
                Layout(host, 1200, 800);
                ((TreeViewItem)window.fileTreeView.ItemContainerGenerator.ContainerFromItem(item)).IsSelected = true;
                Layout(host, 1200, 800);
            }
            TextBox Draft() => Descendants<TextBox>((FrameworkElement)window.commentsDetailsTab.Content)
                .Single(c => AutomationProperties.GetName(c) == "Editable feedback draft");
            window.SetReviewAssignment(assignment);
            window.listBoxStudents.Items.Add(student); window.listBoxStudents.SelectedItem = student;
            Open(first);
            Assert.AreEqual(1, Descendants<InlineCommentAdorner>(host).Count());
            var card = Descendants<InlineCommentAdorner>(host).Single();
            card.IsExpanded = true;
            Layout(host, 1200, 800);
            Descendants<Button>(card).Single(c => Equals(c.Content, "Approve")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(host, 1200, 800);
            StringAssert.Contains(Draft().Text, "Saved pending text");
            Assert.AreEqual(FeedbackReviewStatus.Approved, persistence.LoadComments(first).Single().ReviewStatus);
            Draft().Text += "\nInstructor edit";
            Open(second);
            Assert.IsFalse(Draft().Text.Contains("Saved approved text"));
            card = Descendants<InlineCommentAdorner>(host).Single();
            card.IsExpanded = true;
            Layout(host, 1200, 800);
            Descendants<Button>(card).Single(c => Equals(c.Content, "Approve")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(host, 1200, 800);
            StringAssert.Contains(Draft().Text, "Instructor edit");
            StringAssert.Contains(Draft().Text, "Saved approved text");
            Assert.IsFalse(Draft().Text.Contains("Rejected text"));
            var draft = Draft().Text;
            Open(first); Open(second);
            Assert.AreEqual(draft, Draft().Text);
            window.SetReviewAssignment(new() { Course = "Course", Title = "Another assignment" });
            Layout(host, 1200, 800);
            Assert.AreEqual("", Draft().Text);
            Assert.AreEqual(0, Descendants<InlineCommentAdorner>(host).Count());
        }
        finally { window.Close(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    });

    [TestMethod]
    public void StudentGradesFollowLabAssignmentAndStudentInBothDisplays() => RunSta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "StudentGrades-" + Guid.NewGuid());
        var a = new Student("Alex", "Rivera", "1", Path.Combine(directory, "Rivera_Alex-1"));
        var b = new Student("Jordan", "Chen", "2", Path.Combine(directory, "Chen_Jordan-2"));
        var lab1 = Path.Combine(a.Folder!, "Lab1", "Program.cs");
        var lab2 = Path.Combine(a.Folder!, "Lab2", "Program.cs");
        var other = Path.Combine(b.Folder!, "Lab1", "Program.cs");
        foreach (var path in new[] { lab1, lab2, other })
        { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "// student source"); }
        var database = Path.Combine(directory, "reviews.db");
        var window = new GuidedGrade.MainWindow(new AssignmentPersistenceService(database), new CommentPersistenceService(database));
        try
        {
            var host = (FrameworkElement)window.Content;
            void Open(string path)
            {
                window.fileTreeView.Items.Clear();
                var item = new FileSystemItem(path, false);
                window.fileTreeView.Items.Add(item);
                Layout(host, 1200, 800);
                ((TreeViewItem)window.fileTreeView.ItemContainerGenerator.ContainerFromItem(item)).IsSelected = true;
                Layout(host, 1200, 800);
            }
            string Status() => Descendants<Button>(host).Single(c => AutomationProperties.GetName(c) == "Edit student grade").Content.ToString()!;
            window.listBoxStudents.Items.Add(a); window.listBoxStudents.Items.Add(b);
            window.SetReviewAssignment(new() { Course = "Programming", Title = "Assignment 1" });
            window.listBoxStudents.SelectedItem = a;
            Open(lab1);
            StringAssert.Contains(Status(), "Not graded");
            var firstKey = window.CurrentFeedbackKey();
            window.SaveStudentGrade(firstKey, new(45, 50));
            Layout(host, 1200, 800);
            StringAssert.Contains(Status(), "45 / 50 · 90%");
            Capture(host, "student-grades-before", 1200, 800);
            Assert.IsTrue(Descendants<TextBlock>(window.listBoxStudents).Any(c => c.Text == "Assignment 90%"),
                string.Join(" | ", Descendants<TextBlock>(window.listBoxStudents).Select(c => c.Text)));
            Assert.IsNull(window.GradeForStudent(b));
            Open(lab2);
            Assert.IsNull(window.GradeForStudent(a));
            StringAssert.Contains(Status(), "Not graded");
            window.SaveStudentGrade(window.CurrentFeedbackKey(), new(0, 100));
            Assert.AreEqual(new StudentGrade(45, 150), window.TotalForStudent(a));
            Open(lab1);
            Assert.AreEqual(90, window.GradeForStudent(a)!.Percentage);
            window.listBoxStudents.SelectedItem = b;
            Layout(host, 1200, 800);
            Assert.IsFalse(Descendants<Button>(host).Single(c => AutomationProperties.GetName(c) == "Edit student grade").IsEnabled);
            Open(other);
            window.SaveStudentGrade(window.CurrentFeedbackKey(), new(38, 50));
            Layout(host, 1200, 800);
            StringAssert.Contains(Status(), "76%");
            Capture(host, "student-grades", 1200, 800);
            window.SetReviewAssignment(new() { Course = "Programming", Title = "Assignment 2" });
            Layout(host, 1200, 800);
            Assert.IsNull(window.GradeForStudent(a)); Assert.IsNull(window.GradeForStudent(b));
            Assert.AreEqual(new StudentGrade(45, 150), window.TotalForStudent(a));
            Assert.AreEqual(new StudentGrade(38, 50), window.TotalForStudent(b));
            window.SetReviewAssignment(new() { Course = "Other course", Title = "Assignment 2" });
            Assert.IsNull(window.TotalForStudent(a));
            Layout(host, 1200, 800);
            StringAssert.Contains(Status(), "Not graded");
            Assert.AreEqual(3, new GradePersistenceService(database).LoadAll().Count);
            window.SaveStudentGrade(firstKey, null);
            Assert.AreEqual(2, new GradePersistenceService(database).LoadAll().Count);
        }
        finally { window.Close(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    });

    private static void ClickShell(GuidedGrade.MainWindow window, string name)
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
        var window = new GuidedGrade.MainWindow(new AssignmentPersistenceService(database), new CommentPersistenceService(database));
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
        var window = new GuidedGrade.MainWindow(new AssignmentPersistenceService(database), persistence);
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
            window.CompleteOverallFileReview(pending, window.CurrentFeedbackKey(source), "Overall feedback from the completed job.");
            var reloaded = new CommentPersistenceService(database).LoadComments(source);
            Assert.AreEqual(2, reloaded.Count, "Existing unopened section comments must survive.");
            Assert.AreEqual("Overall feedback from the completed job.", reloaded.Single(item => item.IsOverallReview).Explanation);
            Assert.AreEqual(0, persistence.LoadComments(other).Count);
            var editor = window.codeEditor;
            Assert.AreEqual(File.ReadAllText(other), editor.Text, "Finishing a job must not switch or overwrite the selected file.");
            ((TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(fileItem)).IsSelected = true;
            Layout(host, 1200, 800);
            Assert.IsFalse(Descendants<InlineCommentAdorner>(host).Any(card => card.Feedback.IsOverallReview));
            window.CompleteOverallFileReview(window.CaptureOverallReviewFiles(new[] { source }), window.CurrentFeedbackKey(source), "Updated overall review.");
            Assert.AreEqual(1, persistence.LoadComments(source).Count(item => item.IsOverallReview));
            var inFlight = window.CaptureOverallReviewFiles(new[] { source });
            tree.ContextMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Clear review"))
                .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            window.CompleteOverallFileReview(inFlight, window.CurrentFeedbackKey(source), "Must not return after Clear review.");
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
    public void DelayedReviewsStayWithTheirStudentLabAndAssignment() => RunSta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "QueuedReviews-" + Guid.NewGuid());
        var studentA = new Student("A", "Student", "1", Path.Combine(directory, "StudentA"));
        var studentB = new Student("B", "Student", "2", Path.Combine(directory, "StudentB"));
        var lab1 = Path.Combine(studentA.Folder!, "Lab1", "Program.cs");
        var lab2 = Path.Combine(studentA.Folder!, "Lab2", "Program.cs");
        var other = Path.Combine(studentB.Folder!, "Lab1", "Program.cs");
        foreach (var path in new[] { lab1, lab2, other })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "// " + path);
        }
        var database = Path.Combine(directory, "reviews.db");
        var persistence = new CommentPersistenceService(database);
        var window = new GuidedGrade.MainWindow(new AssignmentPersistenceService(database), persistence);
        try
        {
            var host = (FrameworkElement)window.Content;
            void Open(string path)
            {
                window.fileTreeView.Items.Clear();
                var item = new FileSystemItem(path, false);
                window.fileTreeView.Items.Add(item);
                Layout(host, 1200, 800);
                ((TreeViewItem)window.fileTreeView.ItemContainerGenerator.ContainerFromItem(item)).IsSelected = true;
                Layout(host, 1200, 800);
            }
            string Draft()
            {
                ClickShell(window, "Comments panel");
                var panel = (FrameworkElement)window.commentsDetailsTab.Content;
                Layout(panel, 500, 740);
                return Descendants<TextBox>(panel).Single(c => AutomationProperties.GetName(c) == "Editable feedback draft").Text;
            }
            window.listBoxStudents.Items.Add(studentA);
            window.listBoxStudents.Items.Add(studentB);
            window.listBoxStudents.SelectedItem = studentA;
            window.SetReviewAssignment(new GradingAssignment { Course = "Course", Title = "Assignment 1" });
            Open(lab1);
            var firstTarget = window.CurrentFeedbackKey();
            var firstFiles = window.CaptureOverallReviewFiles([lab1]);
            Open(lab2);
            var secondTarget = window.CurrentFeedbackKey();
            var secondFiles = window.CaptureOverallReviewFiles([lab2]);
            Assert.AreNotEqual(firstTarget, secondTarget);

            window.listBoxStudents.SelectedItem = studentB;
            Assert.AreEqual("", window.codeEditor.Text, "Changing students must detach the old student's editor.");
            Open(other);
            // Deliver the two queued results while a third submission is visible.
            window.CompleteOverallFileReview(secondFiles, secondTarget, "Only A Lab 2");
            window.CompleteOverallFileReview(firstFiles, firstTarget, "Only A Lab 1");
            Assert.AreEqual("", Draft());
            Assert.IsFalse(Descendants<InlineCommentAdorner>(host).Any());
            Assert.AreEqual(0, persistence.LoadComments(other).Count);

            window.listBoxStudents.SelectedItem = studentA;
            Open(lab1);
            Assert.AreEqual("Only A Lab 1", Draft());
            Assert.IsFalse(Descendants<InlineCommentAdorner>(host).Any());
            Assert.IsTrue(Descendants<TextBlock>(host).Any(t => t.Text.Contains("Only A Lab 1")));
            Open(lab2);
            Assert.AreEqual("Only A Lab 2", Draft());

            // Reuse the same physical file with another rubric while old work completes.
            window.SetReviewAssignment(new GradingAssignment { Course = "Course", Title = "Assignment 2" });
            Assert.AreEqual("", Draft());
            window.CompleteOverallFileReview(secondFiles, secondTarget, "Late Assignment 1");
            Layout(host, 1200, 800);
            Assert.IsFalse(Descendants<InlineCommentAdorner>(host).Any());
            Assert.AreEqual("", Draft());
            window.CompleteOverallFileReview(window.CaptureOverallReviewFiles([lab2]), window.CurrentFeedbackKey(), "Assignment 2 result");
            Assert.AreEqual("Assignment 2 result", Draft());
            Assert.AreEqual(2, new CommentPersistenceService(database).LoadComments(lab2).Count);
            window.SetReviewAssignment(new GradingAssignment { Course = "Course", Title = "Assignment 1" });
            Layout(host, 1200, 800);
            Assert.IsFalse(Descendants<InlineCommentAdorner>(host).Any());
            Assert.IsTrue(Descendants<TextBlock>(host).Any(t => t.Text.Contains("Late Assignment 1")));
        }
        finally
        {
            window.Close();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    });

    [TestMethod]
    public void SampleDirectoryRendersRealFilesAndMenus() => RunSta(() =>
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "GuidedGrade.slnx"))) root = root.Parent;
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
        var window = new GuidedGrade.MainWindow(new AssignmentPersistenceService(database), persistence);
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
        Layout((FrameworkElement)provider.Content, 700, 780);
        Assert.IsTrue(Descendants<TextBox>((FrameworkElement)provider.Content)
            .Any(control => AutomationProperties.GetName(control) == "Console model wait timeout"));
        Assert.IsTrue(Descendants<CheckBox>((FrameworkElement)provider.Content)
            .Any(control => Equals(control.Content, "Ask before grading")));
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
            , Deductions = new() { new() { Rule = "External-resource policy violation", Points = 100, RequiresInstructorConfirmation = true } }
        });
        Capture((FrameworkElement)assignment.Content, "assignment", 780, 800);
        for (var page = 1; page < 5; page++)
        {
            assignment.Model.Page.Value = page;
            Flush();
            Capture((FrameworkElement)assignment.Content, "assignment-page-" + page, 780, 800);
        }
        var importModel = new GuidedGrade.ViewModels.AssignmentImportViewModel();
        importModel.Text.Value = "* **10pts:** Item class created.\n* **-10pt deduction:** Missing detailed comments.";
        importModel.Parse();
        using (var importHost = GuidedGrade.Presentation.ReviewTheme.Host(new GuidedGrade.Views.AssignmentImportView(importModel, () => { }, () => { }).Build))
            Capture(importHost, "assignment-import", 760, 760);
        assignment.Close();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(database);
    });

    [TestMethod]
    public void WorkspaceKeepsNativeEditorMenusAndFrameworkPanels() => RunSta(() =>
    {
        var database = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        var window = new GuidedGrade.MainWindow(new AssignmentPersistenceService(database), new CommentPersistenceService(database));
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
            Assert.IsTrue(Descendants<TextBox>((FrameworkElement)queueTab.Content)
                .Any(control => AutomationProperties.GetName(control) == "Task for AI provider"));
            Assert.IsTrue(Descendants<Button>((FrameworkElement)queueTab.Content)
                .Any(button => Equals(button.Content, "Queue task")));
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
            var shell = (FrameworkElement)window.Content;
            Layout(shell, 1000, 700);
            Assert.IsTrue(window.codeEditor.ActualWidth >= 300, "An open panel must leave room to read source.");
            var selectedTab = Descendants<Button>(shell).Single(b => AutomationProperties.GetName(b) == "Rubric panel");
            Assert.AreEqual(true, selectedTab.GetValue(GuidedGrade.Presentation.NativeTheme.TabSelectedProperty));
            selectedTab.ApplyTemplate();
            Layout(shell, 1000, 700);
            var tabBorder = (Border)selectedTab.Template.FindName("tab", selectedTab);
            Assert.AreEqual(new Thickness(0, 0, 0, 2), tabBorder.BorderThickness);
            Assert.AreEqual(Color.FromRgb(0x50, 0xB5, 0xFF), ((SolidColorBrush)tabBorder.BorderBrush).Color);
            var rubricPanel = (FrameworkElement)window.rubricDetailsTab.Content;
            Assert.IsFalse(Descendants<CheckBox>(rubricPanel).Single(t => Equals(t.Content, "Show assignment instructions")).IsChecked == true);
            Capture(shell, "workspace-rubric", 1200, 800);
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
        var dialog = new GuidedGrade.Views.ExtractionProgressDialog();
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
        while (root != null && !File.Exists(Path.Combine(root.FullName, "GuidedGrade.slnx"))) root = root.Parent;
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
