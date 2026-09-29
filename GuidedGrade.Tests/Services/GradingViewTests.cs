using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using GuidedGrade.Models;
using GuidedGrade.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UI_Framework.Wpf;
using ModelTestResult = GuidedGrade.Models.TestResult;

namespace GuidedGrade.Tests.Services;

[TestClass]
[DoNotParallelize]
public class GradingViewTests
{
    [TestMethod]
    public void ScoresDeductionsAndRemarksStayBoundAndEditorsRetainIdentity() => RunSta(() =>
    {
        using var view = new GradingView();
        var student = new Student("Demo", "Student", "sample", null);
        var test = new ModelTestResult { Name = "Ranges", PointsTotal = 100, PointsReceived = 80, Comments = "original" };
        view.LoadResults(new LabResults { Name = "Demo grading", Results = new() { test } }, student);
        Layout(view);
        Assert.AreSame(student, view.CurrentStudent);
        Assert.AreEqual(80f, test.GradedPoints);
        var remarks = Named<TextEditor>(view, "Initial remarks");
        remarks.Text = "<p>Custom feedback survives scoring.</p>";
        var comments = Named<TextEditor>(view, "Comments for Ranges");
        comments.Text = "Edited comment";
        var slider = Named<Slider>(view, "Score for Ranges");
        slider.Value = 90.25;
        Layout(view);
        Assert.AreEqual(90.25f, test.GradedPoints);
        Assert.AreSame(remarks, Named<TextEditor>(view, "Initial remarks"));
        Assert.AreEqual("Edited comment", test.Comments);
        Named<TextBox>(view, "Points for Ranges").Text = "92.";
        Layout(view);
        Assert.AreEqual(92f, test.GradedPoints);
        Assert.AreEqual("92.", Named<TextBox>(view, "Points for Ranges").Text);
        var deduction = Descendants<CheckBox>(view).Single(c => c.Content.ToString()!.StartsWith("Method has multiple returns"));
        deduction.IsChecked = true;
        Layout(view);
        var html = Feedback(view);
        StringAssert.Contains(html, "82.0");
        StringAssert.Contains(html, "Custom feedback survives scoring.");
        StringAssert.Contains(html, "Edited comment");
        FrameworkMigrationTests.Capture(view, "demo-grading-results", 900, 900);
        var external = Descendants<CheckBox>(view).Single(c => c.Content.ToString()!.StartsWith("Violates external"));
        var bad = Descendants<CheckBox>(view).Single(c => c.Content.ToString()!.StartsWith("Bad submission"));
        external.IsChecked = true; Layout(view);
        bad.IsChecked = true; Layout(view);
        Assert.IsFalse(slider.IsEnabled);
        Assert.IsTrue(remarks.IsEnabled);
        StringAssert.Contains(Feedback(view), "24-hour extension");
        bad.IsChecked = false; Layout(view);
        StringAssert.Contains(Feedback(view), "receive a 0");
        Assert.IsTrue(slider.IsEnabled);
        external.IsChecked = false; Layout(view);
        StringAssert.Contains(Feedback(view), "82.0");
    });

    [TestMethod]
    public void SwitchingResultsTargetsNewModelAndClearReleasesNativeControls() => RunSta(() =>
    {
        using var view = new GradingView();
        var first = new ModelTestResult { Name = "Same name", PointsTotal = 10, PointsReceived = 8 };
        var second = new ModelTestResult { Name = "Same name", PointsTotal = 10, PointsReceived = 3 };
        view.LoadResults(new LabResults { Results = new() { first } }); Layout(view);
        var oldEditor = Named<TextEditor>(view, "Comments for Same name");
        view.LoadResults(new LabResults { Results = new() { second } }); Layout(view);
        var editor = Named<TextEditor>(view, "Comments for Same name");
        Assert.AreNotSame(oldEditor, editor);
        editor.Text = "New student only";
        Named<Slider>(view, "Score for Same name").Value = 7; Layout(view);
        Assert.AreEqual(8f, first.GradedPoints);
        Assert.AreEqual("", first.Comments);
        Assert.AreEqual(7f, second.GradedPoints);
        Assert.AreEqual("New student only", second.Comments);
        view.Clear(); Layout(view);
        Assert.IsNull(view.GetResults()); Assert.IsNull(view.CurrentStudent);
        Assert.IsFalse(Descendants<TextEditor>(view).Any());
        view.LoadResults(null); Layout(view);
        Assert.IsTrue(Descendants<TextBlock>(view).Any(t => t.Text == "Results file not found."));
        var host = (ViewHost)view.Content;
        view.Dispose();
        Assert.ThrowsException<ObjectDisposedException>(() => host.Refresh());
    });

    private static string Feedback(GradingView view) => (string)typeof(GradingView)
        .GetMethod("GenerateFeedbackHtml", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, null)!;
    private static T Named<T>(DependencyObject root, string name) where T : DependencyObject =>
        Descendants<T>(root).Single(control => AutomationProperties.GetName(control) == name);
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T found) yield return found;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static void Layout(FrameworkElement element)
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        element.Measure(new Size(900, 900)); element.Arrange(new Rect(0, 0, 900, 900)); element.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); } catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
