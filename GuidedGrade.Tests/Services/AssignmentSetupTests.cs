using GuidedGrade.Models;
using GuidedGrade.Services;
using GuidedGrade.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace GuidedGrade.Tests.Services;
[TestClass]
public class AssignmentSetupTests
{
    private const string Prompt = """
        Act as an expert C++ programming instructor.
        **CRITICAL INSTRUCTION:** Address as you and your.
        Evaluate the provided code against this rubric:
        * **10pts:** Application compiles cleanly, without errors.
        * **10pts:** Item Class created.
        * **10pts:** Define specified fields.
        * **15pts:** Define specified constructors.
        * **10pts:** Accessors and mutators.
        * **15pts:** Other mandated methods.
        * **15pts:** Initialize players.
        * **10pts:** Call existing methods.
        * **5pts:** Follow each TODO.
        Apply the following penalties if applicable:
        * **-10pt deduction:** Multiple return statements.
        * **-10pt deduction:** Missing detailed comments.
        * **-10pt deduction:** Removing existing comments or code.
        * **-10pt deduction:** Runtime crash bugs.
        * **-100pt deduction:** Violates course policy for external resources.
        Provide your final output in the following format:
        1. Score Breakdown
        2. Deductions Applied
        3. Final Grade
        4. Feedback
        """;
    [TestMethod]
    public void ReviewerRolePersistsAndAppearsInSanitizedGradingInstructions()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            var persistence = new AssignmentPersistenceService(path);
            var assignment = new GradingAssignment { Title = "Role test", Rubric = [new("Correctness",100)] };
            assignment.FeedbackOptions.ReviewerRole = "Act as a supportive DSA instructor for PRIVATE_STUDENT.";
            persistence.SaveAssignment(assignment);
            var loaded = persistence.LoadAssignment("General", "Role test")!;
            Assert.AreEqual(assignment.FeedbackOptions.ReviewerRole, loaded.FeedbackOptions.ReviewerRole);
            var snapshot = ReviewContext.Snapshot(loaded)!;
            loaded.FeedbackOptions.ReviewerRole = "Changed after queueing";
            var prompt = OverallFeedbackPrompt.BuildInstructions(snapshot, "", ["PRIVATE_STUDENT"]);
            StringAssert.Contains(prompt, "supportive DSA instructor");
            Assert.IsFalse(prompt.Contains("PRIVATE_STUDENT"));
            Assert.IsFalse(prompt.Contains("Changed after queueing"));
            var model = new GuidedGrade.ViewModels.AssignmentFeedbackViewModel(); model.Load(snapshot.FeedbackOptions);
            Assert.AreEqual(snapshot.FeedbackOptions.ReviewerRole, model.Build().ReviewerRole);
            model.ReviewerRole.Text.Value = "";
            Assert.AreEqual(AssignmentFeedbackOptions.DefaultReviewerRole, model.Build().ReviewerRole);
            model.ReviewerRole.Reset(); Assert.AreEqual(AssignmentFeedbackOptions.DefaultReviewerRole, model.ReviewerRole.Text.Value);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [TestMethod]
    public void ImportsCop2334PercentageAndPointsRubricPreservingMultiFileRequirements()
    {
        var value = AssignmentPromptImporter.Parse("""
            You are an expert C++ programming instructor grading student multi-file submissions for "Lab 1" (COP2334).
            ### Assignment Context & Multi-File Scope
            Review all relevant files together to ensure method declarations match definitions and usages:
            - **A-2 (Creating Methods):** Check GetIndex declaration in .h and definition/usage in .cpp.
            - **B-1 (BuildBoard):** Check const method creation, const vector by reference, and Tile initialization.
            - **C-3 (View Key Support):** Verify the 'v' key, MatchTile, reveal sound, gameOver and winner.
            ### Grading Rubric (100 Points Total)
            - **Part A Lecture Code:** 2% / 2 pts
            - **Part A-1: Calling Methods:** 15% / 15 pts
            - **Part A-2: Creating Methods:** 15% / 15 pts
            - **Part A-3: The Basics:** 15% / 15 pts
            - **Part B Lecture Code:** 2% / 2 pts
            - **Part B-1: Pass by ref, const:** 15% / 15 pts
            - **Part B-2: const:** 15% / 15 pts
            - **Part C Lecture Code:** 1% / 1 pt
            - **Part C-1: Default Parameters:** 10% / 10 pts
            - **Part C-2: Default Parameters (BuildBoard):** 5% / 5 pts
            - **Part C-3: Add a view key support:** 5% / 5 pts
            *Deduction Note:* Apply appropriate point deductions for build/compilation errors or missing multi-file dependencies.
            ### Output Format
            2. Detailed Rubric Breakdown: Grade each section individually with points awarded.
            ### Student Submission Files:
            [Paste student file contents here]
            """);
        Assert.AreEqual(11, value.Rubric.Count);
        Assert.AreEqual(100d, value.TotalMaxPoints);
        Assert.AreEqual("Part B-1: Pass by ref, const", value.Rubric[5].Name);
        StringAssert.Contains(value.Requirements, "GetIndex declaration");
        StringAssert.Contains(value.Requirements, "MatchTile");
        StringAssert.Contains(value.Requirements, "Deduction Note");
        Assert.AreEqual(0, value.Deductions.Count, "An unspecified penalty must not become an invented numeric deduction.");
        Assert.IsFalse(value.Requirements.Contains("Detailed Rubric Breakdown"));
        Assert.IsFalse(value.Requirements.Contains("Paste student file"));
    }

    [TestMethod]
    public void ImportsLabelFirstRubricWithoutMistakingNumberedFeedbackForPoints()
    {
        var value = AssignmentPromptImporter.Parse("""
            ### Grading Rubric (100 Points Total)
            - **Part A Lecture Code:** 2
            - **Part A-1: Calling Methods:** 15
            - **Part A-2: Creating Methods:** 15
            - **Part A-3: The Basics:** 15
            - **Part B Lecture Code:** 2
            - **Part B-1: Pass by ref, const:** 15
            - **Part B-2: const:** 15
            - **Part C Lecture Code:** 1
            - **Part C-1: Completion:** 20 points
            - **Missing comments:** -10 pts
            2. Detailed Rubric Breakdown: Grade each section individually with points awarded and brief justifications considering cross-file implementation (header declarations vs. cpp definitions).
            """);
        Assert.AreEqual(9, value.Rubric.Count);
        Assert.AreEqual(100d, value.TotalMaxPoints);
        Assert.AreEqual("Part A-1: Calling Methods", value.Rubric[1].Name);
        Assert.AreEqual(10d, value.Deductions.Single().Points);
        StringAssert.Contains(value.Requirements, "2. Detailed Rubric Breakdown");
    }

    [TestMethod]
    public void MalformedExplicitPointsStillRequireCorrection()
    {
        Assert.ThrowsException<ArgumentException>(() => AssignmentPromptImporter.Parse("10pts: Valid criterion\n- 5 points missing separator"));
    }

    [TestMethod]
    public void ImportsProvidedFormatWithoutDuplicatingRubricInRequirements()
    {
        var value = AssignmentPromptImporter.Parse(Prompt);
        Assert.AreEqual(9, value.Rubric.Count);
        Assert.AreEqual(100d, value.TotalMaxPoints);
        Assert.AreEqual(5, value.Deductions.Count);
        Assert.IsTrue(value.Deductions.Last().RequiresInstructorConfirmation);
        Assert.IsTrue(value.Deductions[2].RequiresInstructorConfirmation);
        Assert.AreEqual("", value.Requirements);
        var preview = new AssignmentImportViewModel();
        preview.Text.Value = Prompt; preview.Parse();
        Assert.IsTrue(preview.CanApply);
        preview.Text.Value += "changed";
        Assert.IsFalse(preview.CanApply);
    }
    [TestMethod]
    public void StructuredSettingsRoundTripAndQueuedSnapshotStayIndependent()
    {
        var database = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            var store = new AssignmentPersistenceService(database);
            var legacy = new GradingAssignment { Course = "PG2", Title = "Legacy", Rubric = new() { new("Code", 100) } };
            store.SaveAssignment(legacy);
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + database))
            { connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "DELETE FROM AssignmentOptions"; command.ExecuteNonQuery(); }
            Assert.IsTrue(store.LoadAssignment("PG2", "Legacy")!.FeedbackOptions.AddressDirectly);
            var vm = new AssignmentSetupViewModel(store, legacy);
            vm.Import(AssignmentPromptImporter.Parse(Prompt));
            vm.Feedback.Grade.Value = false;
            vm.Class.UseFolderNames.Value = true;
            vm.Save();
            var saved = store.LoadAssignment("pg2", "legacy")!;
            Assert.AreEqual(5, saved.Deductions.Count);
            Assert.IsFalse(saved.FeedbackOptions.IncludeFinalGrade);
            Assert.IsTrue(store.UseFolderNames("PG2"));
            var snapshot = ReviewContext.Snapshot(saved)!;
            saved.Deductions[0].Rule = "changed"; saved.FeedbackOptions.IncludeFinalGrade = true;
            Assert.AreNotEqual(saved.Deductions[0].Rule, snapshot.Deductions[0].Rule);
            Assert.IsFalse(snapshot.FeedbackOptions.IncludeFinalGrade);
            var prompt = OverallFeedbackPrompt.BuildInstructions(snapshot, "");
            StringAssert.Contains(prompt, "INSTRUCTOR CONFIRMATION REQUIRED");
            StringAssert.Contains(prompt, "you and your");
            Assert.IsFalse(prompt.Contains("Final Grade:"));
            Assert.IsFalse(prompt.Contains("Give one total score"));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(database); }
    }
}
