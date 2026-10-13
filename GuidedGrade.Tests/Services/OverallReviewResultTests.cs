using System.Text.Json;
using GuidedGrade.Models;
using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace GuidedGrade.Tests.Services;
[TestClass]
public class OverallReviewResultTests
{
    private static GradingAssignment Assignment() => new() { Rubric = [new("Header", 40), new("Implementation", 60)] };
    private static List<OllamaService.CodeFile> Files() => [new() { Name = "one.h", Content = "void DrawBoard();" }, new() { Name = "two.cpp", Content = "void DrawBoard() { DrawTile(); }" }];
    private static OverallReviewResult.Finding Finding(int id, double earned, int file, string quote) => new() { Id = id, Earned = earned, Status = "verified", File = file, Quote = quote, Reason = "Source implementation present." };
    private static string Reply(params OverallReviewResult.Finding[] criteria) => JsonSerializer.Serialize(new OverallReviewResult.Response { Criteria = criteria.ToList(), Feedback = "Your board drawing calls DrawTile." });
    [TestMethod]
    public void InvalidJsonFindingsRenderNormalOutputAndSeparateWarningDetails()
    {
        var reply = Reply(Finding(1,15,1,"made up quote"));
        var assignment = Assignment(); assignment.Rubric[0].MaxPoints = 2;
        var packed = ReviewWarningEnvelope.Draft(reply, assignment, Files(), "Points exceed rubric maximum.");
        var display = ReviewWarningEnvelope.Unpack(packed);
        StringAssert.Contains(display.Text, "Score Breakdown:");
        StringAssert.Contains(display.Text, "Feedback:");
        Assert.IsFalse(display.Text.Contains("\"criteria\""));
        Assert.IsTrue(display.Warnings.Any(w => w.Contains("maximum 2")));
        Assert.IsTrue(display.Warnings.Any(w => w.Contains("quoted evidence")));
    }

    [TestMethod]
    public async Task InvalidSecondReplyRemainsAnEditableUnverifiedDraft()
    {
        var calls = 0;
        var text = await OverallReviewService.AnalyzeAsync(Assignment(), "source", Files(), (prompt, schema, token) => { calls++; return Task.FromResult("Model prose that needs editing"); });
        Assert.AreEqual(2, calls);
        Assert.IsTrue(ReviewWarningEnvelope.Unpack(text).Warnings.Count > 0);
        StringAssert.Contains(text, "Model prose that needs editing");
    }

    [TestMethod]
    public async Task RetriesMissingCriteriaOnceAndReturnsIncompleteReviewWithoutInventingGrade()
    {
        var calls = 0;
        var output = await OverallReviewService.AnalyzeAsync(Assignment(), "sanitized source", Files(), (prompt, schema, token) =>
        {
            calls++;
            Assert.AreEqual(2, schema!.Value.GetProperty("properties").GetProperty("criteria").GetProperty("minItems").GetInt32());
            if (calls == 2) StringAssert.Contains(prompt, "CORRECTION:");
            return Task.FromResult(Reply(Finding(1,40,1,"void DrawBoard();")));
        });
        Assert.AreEqual(2, calls);
        StringAssert.Contains(output, "Implementation: unverified");
        StringAssert.Contains(output, "Final Grade: 40/100 (suggested; 1 unverified)");
        Assert.IsFalse(output.Contains("Implementation: 0/60"));
    }
    [TestMethod]
    public async Task SuccessfulCorrectionProducesCalculatedGrade()
    {
        var calls = 0;
        var output = await OverallReviewService.AnalyzeAsync(Assignment(), "sanitized source", Files(), (prompt, schema, token) =>
            Task.FromResult(++calls == 1 ? Reply(Finding(1,40,1,"void DrawBoard();")) : Reply(Finding(1,40,1,"void DrawBoard();"),Finding(2,60,2,"DrawTile();"))));
        Assert.AreEqual(2, calls);
        StringAssert.Contains(output, "Final Grade: 100/100");
    }

    [TestMethod]
    public void ComputesTotalFromAllRubricRowsAndCrossFileEvidence()
    {
        var text = OverallReviewResult.Render(Reply(Finding(1,40,1,"void DrawBoard();"), Finding(2,55,2,"DrawTile();")), Assignment(), Files());
        StringAssert.Contains(text, "Final Grade: 95/100");
        StringAssert.Contains(text, "Implementation: 55/60");
    }
    [TestMethod]
    public void UnsupportedSourceEvidenceKeepsVerifiedScore()
    {
        var text = OverallReviewResult.Render(Reply(Finding(1,40,1,"void DrawBoard();"), Finding(2,0,2,"void DrawBoard() {}")), Assignment(), Files());
        StringAssert.Contains(text, "source evidence did not match");
        StringAssert.Contains(text, "Header: 40/40");
        StringAssert.Contains(text, "Final Grade: 40/100 (suggested; 1 unverified)");
        Assert.IsFalse(text.Contains("Implementation: 0/60"));
    }
    [TestMethod]
    public void WithholdsGradeOnlyWhenNoCriterionHasEvidence()
    {
        var text = OverallReviewResult.Render(Reply(Finding(1,40,1,"missing"), Finding(2,60,2,"also missing")), Assignment(), Files());
        StringAssert.Contains(text, "Final Grade: Withheld — no criterion had matching source evidence.");
        Assert.IsFalse(text.Contains("Header: 40/40"));
    }
    [TestMethod]
    public void AcceptsWhitespaceAndCrossFileQuotes()
    {
        var text = OverallReviewResult.Render(Reply(Finding(1,40,1,"void  DrawBoard();"), Finding(2,60,1,"DrawTile();")), Assignment(), Files());
        StringAssert.Contains(text, "Final Grade: 100/100 (suggested)");
    }
    [TestMethod]
    public void CrossFileEvidenceDisplaysTheMatchedFileForCriteriaAndDeductions()
    {
        var assignment = Assignment();
        assignment.Deductions = [new() { Rule = "Penalty", Points = 5 }];
        var response = new OverallReviewResult.Response
        {
            Criteria = [Finding(1,40,2,"void DrawBoard();"), Finding(2,60,0,"DrawTile();")],
            Deductions = [Finding(1,0,999,"DrawTile();")]
        };
        var text = OverallReviewResult.Render(JsonSerializer.Serialize(response), assignment, Files());
        StringAssert.Contains(text, "Header: 40/40 — Source implementation present. [file-1]");
        StringAssert.Contains(text, "Implementation: 60/60 — Source implementation present. [file-2]");
        StringAssert.Contains(text, "-5: Source implementation present. [file-2]");
        Assert.IsFalse(text.Contains("[file-0]"));
        Assert.IsFalse(text.Contains("[file-999]"));
    }
    [TestMethod]
    public void EvidenceMatchingPrefersNominatedFileAndReturnsZeroWhenAbsent()
    {
        var files = Files();
        files.Add(new() { Name = "three.cpp", Content = "DrawTile();" });
        Assert.AreEqual(3, OverallReviewResult.FindEvidenceFile(files, Finding(1,1,3,"DrawTile();")));
        Assert.AreEqual(2, OverallReviewResult.FindEvidenceFile(files, Finding(1,1,99,"DrawTile();")));
        Assert.AreEqual(0, OverallReviewResult.FindEvidenceFile(files, Finding(1,1,2,"missing quote")));
        Assert.AreEqual(0, OverallReviewResult.FindEvidenceFile(files, Finding(1,1,2," ")));
    }
    [TestMethod]
    public void MissingDeductionsArrayDoesNotFailReview()
    {
        var json = JsonSerializer.Serialize(new { criteria = new[] { Finding(1,40,1,"void DrawBoard();"), Finding(2,60,2,"DrawTile();") }, feedback = "ok" });
        var text = OverallReviewResult.Render(json, Assignment(), Files());
        StringAssert.Contains(text, "Final Grade: 100/100 (suggested)");
        StringAssert.Contains(text, "Deductions Applied: None");
    }
    [TestMethod]
    public void InventedDeductionsAreIgnoredWhenNoneAreConfigured()
    {
        var response = new OverallReviewResult.Response
        {
            Criteria = [Finding(1,40,1,"void DrawBoard();"), Finding(2,60,2,"DrawTile();")],
            Deductions = [Finding(1,0,1,"void DrawBoard();")],
            Feedback = "ok"
        };
        var text = OverallReviewResult.Render(JsonSerializer.Serialize(response), Assignment(), Files());
        StringAssert.Contains(text, "Final Grade: 100/100 (suggested)");
        StringAssert.Contains(text, "Deductions Applied: None");
    }
    [TestMethod]
    public void UnverifiedDeductionDoesNotWithholdVerifiedCriteria()
    {
        var assignment = Assignment();
        assignment.Deductions = [new() { Rule = "Policy", Points = 10 }];
        var response = new OverallReviewResult.Response
        {
            Criteria = [Finding(1,40,1,"void DrawBoard();"), Finding(2,60,2,"DrawTile();")],
            Deductions = [new() { Id = 1, Status = "unverified", File = 0, Quote = "", Reason = "Not in source." }],
            Feedback = "ok"
        };
        var text = OverallReviewResult.Render(JsonSerializer.Serialize(response), assignment, Files());
        StringAssert.Contains(text, "unverified evidence; not applied");
        StringAssert.Contains(text, "Final Grade: 100/100 (suggested)");
    }
    [TestMethod]
    public void RejectsMissingDuplicateAndOutOfRangeRubricRows()
    {
        Assert.ThrowsException<InvalidOperationException>(() => OverallReviewResult.Render(Reply(Finding(1,40,1,"void DrawBoard();")), Assignment(), Files()));
        Assert.ThrowsException<InvalidOperationException>(() => OverallReviewResult.Render(Reply(Finding(1,40,1,"void DrawBoard();"),Finding(1,40,1,"void DrawBoard();")), Assignment(), Files()));
        Assert.ThrowsException<InvalidOperationException>(() => OverallReviewResult.Render(Reply(Finding(1,40,1,"void DrawBoard();"),Finding(2,95,2,"DrawTile();")), Assignment(), Files()));
    }
    [TestMethod]
    public void DeductionsUseConfiguredAmountsAndConfirmationFlags()
    {
        var assignment = Assignment();
        assignment.Deductions = [new() { Rule = "Policy", Points = 10, RequiresInstructorConfirmation = true }, new() { Rule = "Rule", Points = 5 }];
        var response = new OverallReviewResult.Response { Criteria = [Finding(1,40,1,"void DrawBoard();"), Finding(2,60,2,"DrawTile();")], Deductions = [Finding(1,0,1,"void DrawBoard();"),Finding(2,0,2,"DrawTile();")] };
        var text = OverallReviewResult.Render(JsonSerializer.Serialize(response), assignment, Files());
        StringAssert.Contains(text, "requires instructor confirmation; not applied");
        StringAssert.Contains(text, "Final Grade: 95/100");
    }
    [TestMethod]
    public void BriefFeedbackIsBoundedByRenderer()
    {
        var assignment = Assignment(); assignment.FeedbackOptions.DetailLevel = 2;
        var response = new OverallReviewResult.Response { Criteria = [Finding(1,40,1,"void DrawBoard();"), Finding(2,60,2,"DrawTile();")], Feedback = string.Join(" ", Enumerable.Repeat("word",200)) };
        var text = OverallReviewResult.Render(JsonSerializer.Serialize(response), assignment, Files());
        Assert.AreEqual(60,text.Split("Feedback: ")[1].Split(' ').Length);
    }
}
