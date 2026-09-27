using System.Net;
using System.Net.Http;
using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public sealed class AzureTransportTests
{
    [TestMethod]
    public async Task SharedTransportKeepsEachRequestsEndpointAndCredentials()
    {
        using var handler = new CaptureHandler();
        using var client = new HttpClient(handler);
        var first = new AzureOpenAIService(client, "https://first.example", "first-key", "first-model");
        var second = new AzureOpenAIService(client, "https://second.example", "second-key", "second-model");
        CollectionAssert.AreEqual(new[] { "ok", "ok" }, await Task.WhenAll(
            first.CompleteAsync("system", "one"), second.CompleteAsync("system", "two")));
        Assert.AreEqual("ok", await first.CompleteAsync("system", "three"));
        CollectionAssert.AreEqual(new[] { "first-key", "second-key", "first-key" }, handler.Keys);
        CollectionAssert.AreEqual(new[] { "first.example", "second.example", "first.example" }, handler.Hosts);
        Assert.IsFalse(client.DefaultRequestHeaders.Contains("api-key"));
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<string> Keys { get; } = new();
        public List<string> Hosts { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Keys.Add(request.Headers.GetValues("api-key").Single());
            Hosts.Add(request.RequestUri!.Host);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}")
            });
        }
    }
}
