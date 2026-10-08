using GuidedGrade.Models;
using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class OverallFeedbackPromptTests
{
    [TestMethod]
    public void BriefFeedbackLimitsWholeReviewRatherThanEachRepeatedSection()
    {
        var assignment = new GradingAssignment { Requirements = "Implement game", Rubric = [new("Part A", 100)] };
        assignment.FeedbackOptions.DetailLevel = 2;
        var prompt = OverallFeedbackPrompt.BuildInstructions(assignment, "");
        StringAssert.Contains(prompt, "at most 60 words");
        StringAssert.Contains(prompt, "at most 20 words");
        StringAssert.Contains(prompt, "No Detailed Review");
        StringAssert.Contains(prompt, "FINAL OUTPUT LENGTH CHECK");
        StringAssert.Contains(prompt, "exact configured rubric names");
        assignment.FeedbackOptions.DetailLevel = 4;
        Assert.IsFalse(OverallFeedbackPrompt.BuildInstructions(assignment, "").Contains("FINAL OUTPUT LENGTH CHECK"));
    }

    [TestMethod]
    public void AssignmentUsesItsRequirementsAndRubricForOneCombinedReview()
    {
        var assignment = new GradingAssignment
        {
            Requirements = "Implement a shop",
            Rubric = new() { new("Correctness", 7.5), new("Validation", 2.5) }
        };
        var instructions = OverallFeedbackPrompt.BuildInstructions(assignment, "Unrelated default requirements");
        assignment.Requirements = "Changed selection";
        assignment.Rubric.Clear();
        var prompt = OverallFeedbackPrompt.WithFiles(instructions, new List<OllamaService.CodeFile>
        {
            new() { Name = "file1.cpp", Content = "void shop() {}" },
            new() { Name = "file2.h", Content = "void shop();" }
        });
        StringAssert.Contains(prompt, "Implement a shop");
        StringAssert.Contains(prompt, "Correctness: 7.5 points");
        StringAssert.Contains(prompt, "Validation: 2.5 points");
        StringAssert.Contains(prompt, "one total score out of 10");
        StringAssert.Contains(prompt, "void shop() {}");
        StringAssert.Contains(prompt, "void shop();");
        StringAssert.Contains(prompt, "withhold the final numeric grade");
        StringAssert.Contains(prompt, "not in Deductions Applied");
        StringAssert.Contains(prompt, "TODO:// labels");
        StringAssert.Contains(prompt, "methods named for those sections");
        StringAssert.Contains(prompt, "alone does not prove completion");
        Assert.IsFalse(prompt.Contains("Unrelated default requirements"));
        Assert.IsFalse(prompt.Contains("Changed selection"));
    }

    [TestMethod]
    public void NoAssignmentUsesDefaultRequirementsWithoutInventingRubric()
    {
        var prompt = OverallFeedbackPrompt.BuildInstructions(null, "Default requirements");
        StringAssert.Contains(prompt, "Default requirements");
        StringAssert.Contains(prompt, "without inventing point values");
        Assert.IsFalse(prompt.Contains("# ASSIGNMENT RUBRIC"));
    }

    [TestMethod]
    public void AssignmentWithoutRubricStillUsesAssignmentRequirements()
    {
        var prompt = OverallFeedbackPrompt.BuildInstructions(new GradingAssignment { Requirements = "Assignment requirements" }, "Default requirements");
        StringAssert.Contains(prompt, "Assignment requirements");
        StringAssert.Contains(prompt, "without inventing point values");
        Assert.IsFalse(prompt.Contains("Default requirements"));
    }
}
