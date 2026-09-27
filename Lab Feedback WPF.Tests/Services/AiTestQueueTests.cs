using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class AiTestQueueTests
{
    [TestMethod]
    public async Task JobsWaitForCleanupAndRunInOrder()
    {
        var queue = new AiTestQueue();
        var cleanup = new TaskCompletionSource();
        var order = new List<int>();
        Assert.IsTrue(queue.TryEnqueue("first", async () =>
        {
            order.Add(1);
            try { await Task.CompletedTask; }
            finally { await cleanup.Task; order.Add(2); }
        }, out var first));
        Assert.IsTrue(queue.TryEnqueue("second", () => { order.Add(3); return Task.CompletedTask; }, out var second));
        CollectionAssert.AreEqual(new[] { 1 }, order);
        Assert.IsFalse(queue.TryEnqueue("FIRST", () => Task.CompletedTask, out _));
        cleanup.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
        Assert.AreEqual(0, queue.Count);
    }

    [TestMethod]
    public async Task FailureDoesNotBlockNextJobAndKeyCanBeReused()
    {
        var queue = new AiTestQueue();
        var release = new TaskCompletionSource();
        queue.TryEnqueue("bad", async () => { await release.Task; throw new InvalidOperationException(); }, out var bad);
        var ran = false;
        queue.TryEnqueue("good", () => { ran = true; return Task.CompletedTask; }, out var good);
        release.SetResult();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => bad);
        await good.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(ran);
        Assert.IsTrue(queue.TryEnqueue("bad", () => Task.CompletedTask, out var retry));
        await retry;
    }

    [TestMethod]
    public async Task CapacityIncludesActiveJob()
    {
        var queue = new AiTestQueue();
        var release = new TaskCompletionSource();
        var jobs = new List<Task>();
        for (var i = 0; i < AiTestQueue.Capacity; i++)
        {
            Assert.IsTrue(queue.TryEnqueue(i.ToString(), () => release.Task, out var job));
            jobs.Add(job);
        }
        Assert.IsFalse(queue.TryEnqueue("overflow", () => Task.CompletedTask, out _));
        release.SetResult();
        await Task.WhenAll(jobs).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, queue.Count);
    }
}
