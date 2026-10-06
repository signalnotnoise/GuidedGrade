using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GuidedGrade.Models;
using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class GradingPayloadTests
{
    private static readonly string[] PrivateValues =
        ["PRIVATE_STUDENT", "PRIVATE_ID", "PRIVATE_COURSE", "PRIVATE_TITLE", "PRIVATE_FILENAME",
         "PRIVATE_SECTION", "PRIVATE_RUNTIME", "PRIVATE_TYPE_INDEX", "PRIVATE_ROUTING_KEY"];

    [DataTestMethod]
    [DataRow(LLMProvider.Ollama)]
    [DataRow(LLMProvider.AzureOpenAI)]
    public async Task SectionHttpPayloadContainsOnlyCriteriaAndSanitizedSource(LLMProvider provider)
    {
        using var handler = new CaptureHandler();
        using var client = new HttpClient(handler);
        var assignment = new GradingAssignment
        {
            Course = "PRIVATE_COURSE", Title = "PRIVATE_TITLE",
            Requirements = "Implement validation. PRIVATE_STUDENT private@example.com",
            Rubric = [new("Correctness", 10)]
        };
        var service = new SectionGradingService(assignment, new LLMSettings
        {
            Provider = provider, AzureEndpoint = "https://example.invalid", AzureApiKey = "test", AzureDeployment = "test"
        }, client);
        var feedback = await service.AnalyzeSectionAsync("PRIVATE_SECTION",
            "// PRIVATE_STUDENT PRIVATE_ID\nint calculate() { return 7; }",
            assignment.Rubric, ["PRIVATE_STUDENT", "PRIVATE_ID"],
            [new RelatedSubmissionFile
            {
                FileName = "PRIVATE_FILENAME.h", FilePath = @"C:\PRIVATE_ROUTING_KEY\PRIVATE_FILENAME.h",
                Content = "// PRIVATE_ID\nstruct Companion {};", DeclaredTypes = ["PRIVATE_TYPE_INDEX"]
            }], "PRIVATE_RUNTIME: exception at C:\\PRIVATE_ROUTING_KEY\\PRIVATE_FILENAME.h");

        Assert.AreEqual("PRIVATE_SECTION", feedback.SectionName, "Local routing metadata is retained locally.");
        AssertPrivateValuesAbsent(handler.Body!);
        var prompt = UserPrompt(handler.Body!);
        StringAssert.Contains(prompt, "Implement validation.");
        StringAssert.Contains(prompt, "Correctness: 10 points");
        StringAssert.Contains(prompt, "int calculate() { return 7; }");
        StringAssert.Contains(prompt, "struct Companion {};");
        Assert.IsFalse(prompt.Contains("private@example.com"));
    }

    [DataTestMethod]
    [DataRow(LLMProvider.Ollama)]
    [DataRow(LLMProvider.AzureOpenAI)]
    public async Task OverallHttpPayloadOmitsFileNamesAndAssignmentMetadata(LLMProvider provider)
    {
        using var handler = new CaptureHandler();
        using var client = new HttpClient(handler);
        var assignment = new GradingAssignment
        {
            Course = "PRIVATE_COURSE", Title = "PRIVATE_TITLE", Requirements = "Check bounds. PRIVATE_STUDENT",
            Rubric = [new("Bounds", 5)]
        };
        var instructions = OverallFeedbackPrompt.BuildInstructions(assignment, "unused", ["PRIVATE_STUDENT"]);
        var files = new List<OllamaService.CodeFile>
        {
            new() { Name = "PRIVATE_FILENAME.cs", Content = "// PRIVATE_ID\nint limit = 10;" }
        };
        var prompt = OverallFeedbackPrompt.WithFiles(instructions, files, ["PRIVATE_ID"]);
        if (provider == LLMProvider.Ollama)
            await new OllamaService(client, "test").AnalyzeCodeAsync(files, prompt, wrapPrompt: false, jobTitle: "PRIVATE_ROUTING_KEY");
        else
            await new AzureOpenAIService(client, "https://example.invalid", "test", "test")
                .AnalyzeCodeAsync(prompt, [new() { FileName = "PRIVATE_FILENAME.cs", Content = files[0].Content }],
                    wrapPrompt: false, jobTitle: "PRIVATE_ROUTING_KEY");
        AssertPrivateValuesAbsent(handler.Body!);
        StringAssert.Contains(UserPrompt(handler.Body!), "Check bounds.");
        StringAssert.Contains(UserPrompt(handler.Body!), "int limit = 10;");
    }

    private static void AssertPrivateValuesAbsent(string body)
    {
        foreach (var value in PrivateValues)
            Assert.IsFalse(body.Contains(value, StringComparison.Ordinal), $"Payload leaked {value}.");
    }

    private static string UserPrompt(string body)
    {
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"message":{"content":"SCORE: 7"},"choices":[{"message":{"content":"SCORE: 7"}}]}""", Encoding.UTF8, "application/json")
            };
        }
    }
}
