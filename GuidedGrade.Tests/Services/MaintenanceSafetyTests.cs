using System.IO;
using GuidedGrade.Services;
using GuidedGrade.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace GuidedGrade.Tests.Services;
[TestClass]
public class MaintenanceSafetyTests
{
    [TestMethod]
    public async Task OversizedInputIsRejectedWithoutPartialSource()
    {
        var path = Path.GetTempFileName();
        try { await File.WriteAllTextAsync(path, "12345"); await Assert.ThrowsExceptionAsync<IOException>(() => BoundedTextReader.ReadAsync(path, default, 4)); }
        finally { File.Delete(path); }
    }
    [TestMethod]
    public void CacheEvictsOldEntriesAndHandlesCaseInsensitiveTouches()
    {
        var cache = new BoundedCache<int>(2, () => "pinned");
        cache["pinned"] = 1; cache["old"] = 2; cache["OLD"] = 3; cache["new"] = 4;
        Assert.AreEqual(2, cache.Count); Assert.IsFalse(cache.TryGetValue("old", out _)); Assert.AreEqual(1, cache["PINNED"]);
    }
    [TestMethod]
    public void DraftSurvivesReopeningIncludingAnEmptyDraft()
    {
        var path = Path.GetTempFileName();
        try { var first = new ReviewDraftStore(path); first["assignment/student"] = "Your edited feedback"; first["empty"] = "";
            var second = new ReviewDraftStore(path); Assert.AreEqual("Your edited feedback", second["assignment/student"]); Assert.IsTrue(second.ContainsKey("empty")); }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }
    [TestMethod]
    public void LegacyKeysAreMigratedAndProtectedAtRest()
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, "{\"AzureApiKey\":\"secret-test-key\"}"); var settings = ProtectedSettingsStore.Load(path);
            Assert.AreEqual("secret-test-key", settings.AzureApiKey); Assert.IsFalse(File.ReadAllText(path).Contains("secret-test-key"));
            Assert.AreEqual("secret-test-key", ProtectedSettingsStore.Load(path).AzureApiKey); }
        finally { File.Delete(path); }
    }
    [TestMethod]
    public async Task UnsupportedProviderCannotFallThroughToOllama()
    {
        await Assert.ThrowsExceptionAsync<NotSupportedException>(() => LlmCompletionService.CompleteAsync(new() { Provider = LLMProvider.OpenAI }, "system", "input"));
        await Assert.ThrowsExceptionAsync<NotSupportedException>(() => LlmCompletionService.CompleteAsync(new() { Provider = (LLMProvider)999 }, "system", "input"));
    }
    [TestMethod]
    public void MultilineSignaturesAndBracesInsideCommentsAreHandled()
    {
        var sections = CodeSectionDetector.Extract("/* void Fake() { } */\nvoid Example(\n int value) const\n{\n // }\n auto text = \"}\";\n value++;\n}", "source");
        Assert.AreEqual(1, sections.Count); Assert.AreEqual("Example", sections[0].Name); StringAssert.Contains(sections[0].Code, "value++");
    }
    [TestMethod]
    public void FolderTraversalReportsItsLimit()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try { File.WriteAllText(Path.Combine(root,"a.cpp"), ""); File.WriteAllText(Path.Combine(root,"b.cpp"), "");
            var node = new FileSystemItem(root, true); var warnings = SubmissionFolderLoader.Populate(node, root, 1);
            Assert.AreEqual(1, node.Children.Count); Assert.AreEqual(1, warnings.Count); }
        finally { Directory.Delete(root, true); }
    }
    [TestMethod]
    public async Task MultiFileReviewSendsOneAnonymousCombinedRequest()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var header = Path.Combine(root, "StudentName.h"); var source = Path.Combine(root, "StudentName.cpp");
            File.WriteAllText(header, "// StudentName\nvoid Run();"); File.WriteAllText(source, "void Run() {}");
            var calls = 0;
            var reply = await ReviewOrchestrator.ReviewAsync(new(), new(), [header, source], ["StudentName"], complete: (prompt, schema, ct) =>
            {
                calls++; StringAssert.Contains(prompt, "void Run();"); StringAssert.Contains(prompt, "void Run() {}");
                Assert.IsFalse(prompt.Contains(root)); Assert.IsFalse(prompt.Contains("StudentName")); return Task.FromResult("Your feedback");
            });
            Assert.AreEqual(1, calls); Assert.AreEqual("Your feedback", reply);
        }
        finally { Directory.Delete(root, true); }
    }
    [TestMethod]
    public async Task MissingFileCancelsBeforeAnyModelRequest()
    {
        var calls = 0;
        await Assert.ThrowsExceptionAsync<FileNotFoundException>(() => ReviewOrchestrator.ReviewAsync(new(), new(),
            [Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())], [], complete: (prompt, schema, ct) => { calls++; return Task.FromResult(""); }));
        Assert.AreEqual(0, calls);
    }

}
