using System.Net;
using System.Net.Http;
using System.Text.Json;
using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class LlmHttpFailureTests
{
    [TestMethod]
    public async Task GpuOutOfMemoryRetriesOnceOnCpuAndPreservesStructuredRequest()
    {
        using var handler = new ReplyHandler((call, _) => call == 1
            ? Failure("CUDA error: out of memory") : Success());
        using var client = new HttpClient(handler);
        var result = await new OllamaService(client, "test-model").CompleteAsync("system", "prompt",
            responseSchema: ConsoleDriverAgent.ActionSchema);
        Assert.AreEqual("ok", result);
        Assert.AreEqual(2, handler.Bodies.Count);
        using var original = JsonDocument.Parse(handler.Bodies[0]);
        using var fallback = JsonDocument.Parse(handler.Bodies[1]);
        var root = fallback.RootElement;
        Assert.AreEqual(0, root.GetProperty("options").GetProperty("num_gpu").GetInt32());
        Assert.AreEqual(0, root.GetProperty("options").GetProperty("temperature").GetInt32());
        Assert.AreEqual(original.RootElement.GetProperty("format").GetRawText(), root.GetProperty("format").GetRawText());
        Assert.AreEqual(original.RootElement.GetProperty("messages").GetRawText(), root.GetProperty("messages").GetRawText());
    }

    [TestMethod]
    public async Task UnrelatedServerErrorRetainsDetailsAndIsNotRetried()
    {
        using var handler = new ReplyHandler((_, _) => Failure("runner startup failed"));
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsExceptionAsync<HttpRequestException>(() =>
            new OllamaService(client, "test-model").CompleteAsync("system", "prompt"));
        Assert.AreEqual(HttpStatusCode.InternalServerError, error.StatusCode);
        StringAssert.Contains(error.Message, "runner startup failed");
        StringAssert.Contains(error.Message, "test-model");
        Assert.AreEqual(1, handler.Bodies.Count);
    }

    [TestMethod]
    public async Task CpuFallbackFailureStopsAfterSecondAttempt()
    {
        using var handler = new ReplyHandler((_, _) => Failure("CUDA error: out of memory"));
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsExceptionAsync<HttpRequestException>(() =>
            new OllamaService(client, "test-model").CompleteAsync("system", "prompt"));
        StringAssert.Contains(error.Message, "CPU fallback also failed");
        Assert.AreEqual(2, handler.Bodies.Count);
    }

    [TestMethod]
    public async Task CancellationPreventsFallback()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new ReplyHandler((_, _) =>
        {
            cancellation.Cancel();
            return Failure("CUDA error: out of memory");
        });
        using var client = new HttpClient(handler);
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() =>
            new OllamaService(client, "test-model").CompleteAsync("system", "prompt", cancellation.Token));
        Assert.AreEqual(1, handler.Bodies.Count);
    }

    [TestMethod]
    public void ExtractsAzureMessageAndBoundsNonJsonErrors()
    {
        Assert.AreEqual("deployment unavailable", LlmHttpErrors.Detail("{\"error\":{\"message\":\"deployment unavailable\"}}"));
        Assert.AreEqual(4000, LlmHttpErrors.Detail(new string('x', 10000)).Length);
    }

    private static HttpResponseMessage Failure(string error) => new(HttpStatusCode.InternalServerError)
        { Content = new StringContent(JsonSerializer.Serialize(new { error })) };
    private static HttpResponseMessage Success() => new(HttpStatusCode.OK)
        { Content = new StringContent("{\"message\":{\"content\":\"ok\"}}") };

    private sealed class ReplyHandler(Func<int, CancellationToken, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return reply(Bodies.Count, cancellationToken);
        }
    }
}
