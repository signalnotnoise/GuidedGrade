using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class RuntimeTerminalPresenterTests
{
    [TestMethod]
    public void TerminalBoundsTextAndVisualObjectsAndResetsBetweenJobs()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var view = new RichTextBox();
                using var presenter = new RuntimeTerminalPresenter(view);
                using var old = presenter.BeginSession();
                for (var i = 0; i < 2000; i++)
                    presenter.Append("a", i % 2 == 0 ? Brushes.Tomato : Brushes.LightGray);
                Assert.IsTrue(presenter.RetainedRuns <= OutputLimits.TerminalRuns);
                presenter.Append(new string('x', 200_000) + "LATEST", Brushes.LightGray);
                Assert.IsTrue(presenter.RetainedCharacters <= OutputLimits.TerminalCharacters);
                StringAssert.Contains(new TextRange(view.Document.ContentStart, view.Document.ContentEnd).Text, "LATEST");
                Assert.IsFalse(view.IsUndoEnabled);
                Assert.IsFalse(view.CanUndo);
                using var next = presenter.BeginSession();
                old.Report(new ConsoleProgress { Text = "STALE" });
                next.Report(new ConsoleProgress { Text = "CURRENT", IsStderr = true });
                presenter.Flush();
                var text = new TextRange(view.Document.ContentStart, view.Document.ContentEnd).Text;
                StringAssert.Contains(text, "CURRENT");
                Assert.IsFalse(text.Contains("STALE") || text.Contains("LATEST"));
                Assert.AreEqual(Brushes.Tomato, ((Run)((Paragraph)view.Document.Blocks.LastBlock).Inlines.LastInline).Foreground);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "Terminal test did not finish.");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
