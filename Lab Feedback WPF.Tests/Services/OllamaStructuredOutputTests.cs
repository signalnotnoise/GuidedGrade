using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class OllamaStructuredOutputTests
{
    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("{\"message\":null}")]
    [DataRow("{\"message\":{}}")]
    [DataRow("{\"message\":{\"content\":null}}")]
    public async Task MissingResponseContentUsesExistingFallback(string reply)
    {
        using var handler = new CaptureHandler { Reply = reply };
        using var client = new HttpClient(handler);
        var service = new OllamaService(client, "test-model");
        Assert.AreEqual("No response from model.", await service.CompleteAsync("system", "prompt"));
    }

    [TestMethod]
    public async Task ConsoleRequestSendsSchemaAndReturnsActionWhilePlainRequestsRemainText()
    {
        using var handler = new CaptureHandler();
        using var client = new HttpClient(handler);
        var service = new OllamaService(client, "test-model");
        var result = await service.CompleteAsync("system", "prompt", responseSchema: ConsoleDriverAgent.ActionSchema);
        using (var request = JsonDocument.Parse(handler.Body!))
        {
            var root = request.RootElement;
            Assert.AreEqual("/api/chat", handler.Path);
            Assert.AreEqual("test-model", root.GetProperty("model").GetString());
            Assert.AreEqual(0, root.GetProperty("options").GetProperty("temperature").GetInt32());
            var schema = root.GetProperty("format");
            Assert.AreEqual(JsonValueKind.Object, schema.ValueKind);
            Assert.AreEqual(4, schema.GetProperty("required").GetArrayLength());
            Assert.IsFalse(schema.GetProperty("additionalProperties").GetBoolean());
            CollectionAssert.AreEqual(new[] { "type", "wait", "close", "stop" },
                schema.GetProperty("properties").GetProperty("action").GetProperty("enum")
                    .EnumerateArray().Select(x => x.GetString()).ToArray());
        }
        Assert.AreEqual("1", ConsoleDriverAgent.Parse(result, 0).ResolvedInput);
        await service.CompleteAsync("system", "feedback");
        using var plain = JsonDocument.Parse(handler.Body!);
        Assert.IsFalse(plain.RootElement.TryGetProperty("format", out _));
        Assert.IsFalse(plain.RootElement.TryGetProperty("options", out _));
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body;
        public string? Path;
        public string? Reply;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Path = request.RequestUri!.AbsolutePath;
            var reply = JsonSerializer.Serialize(new { message = new { content = "{\"action\":\"type\",\"input\":\"1\",\"reason\":\"Choose buy\",\"observation\":\"ok\"}" } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Reply ?? reply, Encoding.UTF8, "application/json") };
        }
    }
}
