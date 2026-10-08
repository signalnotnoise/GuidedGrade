using GuidedGrade.Models;
using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class BatchReviewTests
{
    [TestMethod]
    public async Task ClearsSelectedFilesThenOptionallyBuildsThenReviewsSubmissionOnce()
    {
        var item = new BatchReviewItem(new Student("A", "Student", "1", "A"), "one", "entry", "context", null) { Files = ["one", "two"] };
        foreach (var build in new[] { false, true })
        {
            var calls = new List<string>();
            await BatchReviewPlan.ProcessStudentAsync(item, build, path => { calls.Add("clear " + path); return 7; },
                _ => { calls.Add("build"); return Task.CompletedTask; },
                async (files, token) => { CollectionAssert.AreEqual(new[] { "one", "two" }, files.Select(file => file.Path).ToArray()); Assert.IsTrue(files.All(file => file.Version == 7)); calls.Add("review submission"); await Task.Yield(); }, CancellationToken.None);
            CollectionAssert.AreEqual(build ? new[] { "clear one", "clear two", "build", "review submission" } :
                new[] { "clear one", "clear two", "review submission" }, calls);
        }
    }

    [TestMethod]
    public void ReplacementRemovesGeneratedDraftBlocksButKeepsInstructorEdits()
    {
        var comment = new SectionFeedback { IsOverallReview = true, Explanation = "Old review" };
        Assert.AreEqual("Instructor note", ReviewDraftCleanup.Remove("Old review\n\n---\n\nInstructor note", [comment]));
        Assert.AreEqual("", ReviewDraftCleanup.Remove("Old review", [comment]));
        Assert.AreEqual("Edited old review", ReviewDraftCleanup.Remove("Edited old review", [comment]));
    }

    [TestMethod]
    public void RecursivePrefixMatchesZeroOrManyFoldersAndSelectsNewestCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), "NestedBatch-" + Guid.NewGuid());
        var a = new Student("A", "Student", "1", Path.Combine(root, "A"));
        var b = new Student("B", "Student", "2", Path.Combine(root, "B"));
        var c = new Student("C", "Student", "3", Path.Combine(root, "C"));
        const string suffix = "Lab_2_Conversions/Lab 2/StudentWork.h";
        void Write(string folder, string file)
        {
            var path = Path.Combine(folder, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "// code");
        }
        try
        {
            Write(a.Folder!, suffix);
            Write(b.Folder!, "Extra/Another/" + suffix);
            Write(c.Folder!, "Copy1/" + suffix); Write(c.Folder!, "Copy2/" + suffix);
            Write(c.Folder!, "Copy3/" + suffix);
            var modified = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(Path.Combine(c.Folder!, "Copy1", suffix), modified);
            File.SetLastWriteTimeUtc(Path.Combine(c.Folder!, "Copy2", suffix), modified.AddDays(-1));
            File.SetLastWriteTimeUtc(Path.Combine(c.Folder!, "Copy3", suffix), modified.AddDays(1));
            Write(b.Folder!, "Extra/Another/Lab_2_Conversions/Run.sln");
            var assignment = new GradingAssignment { Title = "Conversions" };
            var plan = BatchReviewPlan.Create([a, b, c], assignment, @"**\Lab_2_Conversions\Lab 2\StudentWork.h", "");
            Assert.IsNull(plan[0].SkipReason); Assert.IsNull(plan[1].SkipReason);
            Assert.AreEqual(Path.Combine(b.Folder!, "Extra", "Another", "Lab_2_Conversions", "Lab 2", "StudentWork.h"), plan[1].File);
            Assert.IsNull(plan[2].SkipReason);
            Assert.AreEqual(Path.GetFullPath(Path.Combine(c.Folder!, "Copy3", suffix)), plan[2].File);
            Write(c.Folder!, "Copy1/Run.sln"); Write(c.Folder!, "Copy3/Run.sln");
            File.SetLastWriteTimeUtc(Path.Combine(c.Folder!, "Copy1", "Run.sln"), modified.AddDays(5));
            Assert.AreEqual(Path.Combine(c.Folder!, "Copy3", "Run.sln"),
                BatchReviewPlan.Create([c], assignment, "**/" + suffix, "**/Run.sln")[0].EntryPoint);
            File.SetLastWriteTimeUtc(Path.Combine(c.Folder!, "Copy1", suffix), modified.AddDays(1));
            Assert.AreEqual(Path.GetFullPath(Path.Combine(c.Folder!, "Copy1", suffix)),
                BatchReviewPlan.Create([c], assignment, "**/" + suffix, "")[0].File);
            Assert.IsNull(BatchReviewPlan.Create([b], assignment, "**/" + suffix, "**/Lab_2_Conversions/Run.sln")[0].SkipReason);
            StringAssert.Contains(BatchReviewPlan.Create([a], assignment, "**/Missing.h", "")[0].SkipReason!, "missing");
            Assert.ThrowsException<ArgumentException>(() => BatchReviewPlan.Create([a], assignment, "**/../B/File.h", ""));
            Assert.ThrowsException<ArgumentException>(() => BatchReviewPlan.Create([a], assignment, "**/Lab*/*.h", ""));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void PreviewMapsEachStudentToOwnFileAndFlagsMissingFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "Batch-" + Guid.NewGuid());
        var a = new Student("A", "Student", "1", Path.Combine(root, "A"));
        var b = new Student("B", "Student", "2", Path.Combine(root, "B"));
        try
        {
            Directory.CreateDirectory(Path.Combine(a.Folder!, "Lab1"));
            File.WriteAllText(Path.Combine(a.Folder!, "Lab1", "Main.cs"), "code");
            File.WriteAllText(Path.Combine(a.Folder!, "Lab1", "Helpers.cs"), "helpers");
            var assignment = new GradingAssignment { Course = "Course", Title = "Assignment" };
            var plan = BatchReviewPlan.Create([a, b], assignment, "Lab1/Main.cs", "");
            Assert.IsNull(plan[0].SkipReason);
            Assert.IsNull(BatchReviewPlan.Create([a], assignment, "Lab1/Main.cs", "Missing/Bad*.sln", buildAndRun: false)[0].SkipReason);
            Assert.AreEqual("Review file missing", plan[1].SkipReason);
            Assert.AreNotEqual(plan[0].Context, plan[1].Context);
            Assert.AreEqual(ReviewContext.Key(a.Folder, assignment, plan[0].File), plan[0].Context);
            var multiple = BatchReviewPlan.Create([a, b], assignment, "Lab1/Main.cs\nLab1/Helpers.cs\nLab1/Main.cs", "Lab1/Main.cs");
            Assert.AreEqual(2, multiple[0].Files.Count);
            Assert.IsNull(multiple[0].SkipReason);
            Assert.IsNotNull(multiple[1].SkipReason);
            Assert.ThrowsException<ArgumentException>(() => BatchReviewPlan.Create([a], assignment, "Lab1/Main.cs\nLab2/Main.cs", ""));
            Assert.ThrowsException<ArgumentException>(() => BatchReviewPlan.Create([a], assignment, "../B/Main.cs", ""));
            Assert.ThrowsException<ArgumentException>(() => BatchReviewPlan.Create([a], assignment, "Lab1/Main.cs", "Lab2/Main.cs"));
            Assert.ThrowsException<ArgumentException>(() => BatchReviewPlan.Create([a], assignment, plan[0].File!, ""));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task LoopSkipsMissingFilesContinuesAfterErrorsAndStopsOnCancellation()
    {
        var student = new Student("A", "Student", "1", "A");
        var plan = new[] {
            new BatchReviewItem(student, "missing", "missing", "1", "Missing"),
            new BatchReviewItem(student, "fails", "fails", "2", null),
            new BatchReviewItem(student, "works", "works", "3", null) };
        var visited = new List<string>(); var statuses = new List<string>();
        await BatchReviewPlan.RunAsync(plan, async (item, token) =>
        {
            visited.Add(item.File!); await Task.Yield();
            if (item.File == "fails") throw new IOException("Test failure");
        }, (_, status) => statuses.Add(status), CancellationToken.None);
        CollectionAssert.AreEqual(new[] { "fails", "works" }, visited);
        Assert.IsTrue(statuses.Contains("Skipped: Missing"));
        Assert.IsTrue(statuses.Contains("Failed: Test failure"));
        Assert.AreEqual("Finished", statuses.Last());
        using var cancellation = new CancellationTokenSource();
        visited.Clear();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => BatchReviewPlan.RunAsync(plan, (item, token) =>
        {
            visited.Add(item.File!); cancellation.Cancel(); return Task.CompletedTask;
        }, (_, _) => { }, cancellation.Token));
        CollectionAssert.AreEqual(new[] { "fails" }, visited);
    }
}
