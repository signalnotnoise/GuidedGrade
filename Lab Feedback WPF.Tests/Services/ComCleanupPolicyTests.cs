using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public sealed class ComCleanupPolicyTests
{
    [TestMethod]
    public void FailedIdleCleanupIsReportedAndRememberedAfterRecovery() => RunSta(() =>
    {
        var attempts = 0;
        var reported = new List<Exception>();
        var policy = new ApplicationComCleanupPolicy(Dispatcher.CurrentDispatcher,
            () => { if (++attempts == 1) throw new InvalidOperationException("injected failure"); }, reported.Add);
        policy.RequestCleanup();
        Flush();
        Assert.AreEqual(1, reported.Count);
        Assert.AreEqual(1, policy.FailureCount);
        policy.RequestCleanup();
        Flush();
        Assert.IsNull(policy.LastError);
        Assert.AreEqual(1, policy.FailureCount, "Recovery must not erase a transient failure from app diagnostics.");
        policy.CloseBeforeDispatcherShutdown();
        Assert.IsTrue(policy.IsClosed);
    });

    [TestMethod]
    public void UpdateBoundaryPreservesTextSelectionUndoAndInputSupport() => RunSta(() =>
    {
        var policy = new ApplicationComCleanupPolicy(Dispatcher.CurrentDispatcher);
        var editor = new TextBox { Text = "original text", IsUndoEnabled = true };
        using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Cleanup editor regression")
        {
            Width = 400, Height = 200, PositionX = -10000, PositionY = -10000,
            WindowStyle = unchecked((int)0x80000000)
        });
        source.RootVisual = editor;
        editor.Measure(new Size(400, 200));
        editor.Arrange(new Rect(0, 0, 400, 200));
        editor.UpdateLayout();
        editor.Select(0, 8);
        policy.RunUpdate(() => { editor.BeginChange(); try { editor.SelectedText = "edited"; } finally { editor.EndChange(); } });
        editor.Select(1, 3);
        Flush();
        Assert.AreEqual("edited text", editor.Text);
        Assert.AreEqual(1, editor.SelectionStart);
        Assert.AreEqual(3, editor.SelectionLength);
        Assert.IsTrue(System.Windows.Input.InputMethod.GetIsInputMethodEnabled(editor));
        Assert.IsTrue(editor.CanUndo);
        policy.RunUpdate(() => editor.Undo());
        Assert.AreEqual("original text", editor.Text);
        source.RootVisual = null;
        source.Dispose();
        policy.CloseBeforeDispatcherShutdown();
    });

    private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "Cleanup test timed out.");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
