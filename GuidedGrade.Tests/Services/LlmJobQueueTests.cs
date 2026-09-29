using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class LlmJobQueueTests
{
    [TestMethod]
    public async Task AssignmentsOvertakeWaitingGeneralJobsWithoutInterruptingActiveJob()
    {
        var queue = new LlmJobQueue();
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        var active = queue.EnqueueAsync(_ => release.Task);
        Task<string> Add(string name, LlmJobPriority priority) => queue.EnqueueAsync(_ =>
        {
            order.Add(name);
            return Task.FromResult(name);
        }, priority);
        var general = Add("general", LlmJobPriority.General);
        var assignment1 = Add("assignment1", LlmJobPriority.Assignment);
        var assignment2 = Add("assignment2", LlmJobPriority.Assignment);
        Assert.AreEqual(0, order.Count);
        release.SetResult("active");
        await Task.WhenAll(active, general, assignment1, assignment2).WaitAsync(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(new[] { "assignment1", "assignment2", "general" }, order);
        Assert.AreEqual(0, queue.Count);
    }

    [TestMethod]
    public async Task CancellingWaitingJobImmediatelyFreesCapacityAndNeverExecutesIt()
    {
        var queue = new LlmJobQueue();
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = queue.EnqueueAsync(_ => release.Task);
        var ran = false;
        var pending = queue.EnqueueAsync(_ => { ran = true; return Task.FromResult(""); }, title: "cancel me");
        queue.Cancel(queue.Snapshot().Single(job => job.Title == "cancel me").Id);
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await pending);
        Assert.AreEqual(1, queue.Count);
        Assert.IsFalse(ran);
        release.SetResult("");
        await active;
        Assert.IsFalse(ran);
        Assert.AreEqual("Cancelled", queue.Snapshot().Single(job => job.Title == "cancel me").Status);
    }

    [TestMethod]
    public async Task ActiveCancellationWaitsForCleanupBeforeNextJob()
    {
        var queue = new LlmJobQueue();
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = queue.EnqueueAsync(async token =>
        {
            try { await Task.Delay(Timeout.Infinite, token); return ""; }
            finally { cancelled.SetResult(); await cleanup.Task; }
        });
        var ran = false;
        var next = queue.EnqueueAsync(_ => { ran = true; return Task.FromResult(""); });
        queue.Cancel(queue.Snapshot().Single(job => job.Status == "Running").Id);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(ran);
        Assert.AreEqual("Cancelling", queue.Snapshot()[0].Status);
        cleanup.SetResult();
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await active);
        await next.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(ran);
    }

    [TestMethod]
    public async Task CallerCancellationRemovesWaitingRequestWithoutWaitingForActiveProvider()
    {
        var queue = new LlmJobQueue();
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = queue.EnqueueAsync(_ => release.Task);
        using var cancellation = new CancellationTokenSource();
        var pending = queue.EnqueueAsync(_ => throw new AssertFailedException("Cancelled work ran"),
            cancellationToken: cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(1, queue.Count);
        release.SetResult("");
        await active;
    }

    [TestMethod]
    public async Task FailureContinuesAndHistoryIsBounded()
    {
        var queue = new LlmJobQueue();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            queue.EnqueueAsync(_ => throw new InvalidOperationException("test failure")));
        Assert.AreEqual("Failed", queue.Snapshot()[0].Status);
        for (var i = 0; i < 60; i++) await queue.EnqueueAsync(_ => Task.FromResult("done"));
        Assert.AreEqual(50, queue.Snapshot().Count);
        Assert.AreEqual(0, queue.Count);
        queue.ClearFinished();
        Assert.AreEqual(0, queue.Snapshot().Count);
    }
}
