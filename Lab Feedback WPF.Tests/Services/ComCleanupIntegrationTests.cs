using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public sealed class ComCleanupIntegrationTests
{
    [TestMethod]
    public void AppOwnsOnePackagedCleanupPolicy() => RunSta(() =>
    {
        var app = new Lab_Feedback_WPF.App();
        app.ConfigureCleanup();
        Assert.IsTrue(app.CleanupEnabled);
        Assert.AreEqual(0, app.CleanupFailureCount);
        Assert.ThrowsException<InvalidOperationException>(app.ConfigureCleanup);
        var field = typeof(Lab_Feedback_WPF.App).GetField("cleanupPolicy",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var policy = field.GetValue(app)!;
        policy.GetType().GetMethod("CloseBeforeDispatcherShutdown")!.Invoke(policy, null);
        Assert.IsTrue((bool)policy.GetType().GetProperty("IsClosed")!.GetValue(policy)!);
    });

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
            finally { System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "Cleanup integration test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
