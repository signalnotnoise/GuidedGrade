using GuidedGrade.Models;
using GuidedGrade.Services;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class ReviewContextTests
{
    [TestMethod]
    public void WindowsPathCasingDoesNotHideOrDuplicateSavedComments()
    {
        var database = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            var service = new CommentPersistenceService(database);
            service.SaveComments("C:\\Student\\Program.cs", [new SectionFeedback { SectionName = "First" }]);
            Assert.AreEqual(1, service.LoadComments("c:\\student\\program.cs").Count);
            service.SaveComments("c:\\student\\program.cs", [new SectionFeedback { SectionName = "Updated" }]);
            Assert.AreEqual("Updated", service.LoadComments("C:\\STUDENT\\PROGRAM.CS").Single().SectionName);
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(database); }
    }

    [TestMethod]
    public void DraftsSeparateStudentsLabsAndAssignmentsButKeepNestedFilesTogether()
    {
        var student = Path.Combine(Path.GetTempPath(), "StudentA");
        var assignment = new GradingAssignment { Course = "Programming", Title = "Lab 1" };
        string Key(string file) => ReviewContext.Key(student, assignment, Path.Combine(student, file));
        var first = Key("Lab1/Program.cs");
        Assert.AreEqual(first, Key("Lab1/Headers/Types.h"));
        Assert.AreNotEqual(first, Key("Lab2/Program.cs"));
        Assert.AreNotEqual(first, ReviewContext.Key(student + "B", assignment, Path.Combine(student + "B", "Lab1/Program.cs")));
        assignment.Title = "Lab 2";
        Assert.AreNotEqual(first, Key("Lab1/Program.cs"));
        Assert.IsFalse(ReviewContext.Contains(student, student + "Other/Program.cs"));
    }

    [TestMethod]
    public void CapturedRubricSurvivesEditingTheOriginalAssignment()
    {
        var assignment = new GradingAssignment { Requirements = "Original", Rubric = [new("Correctness", 10)] };
        var captured = ReviewContext.Snapshot(assignment)!;
        assignment.Requirements = "Another lab";
        assignment.Rubric[0].MaxPoints = 99;
        assignment.Rubric.Clear();
        Assert.AreEqual("Original", captured.Requirements);
        Assert.AreEqual(10, captured.Rubric.Single().MaxPoints);
    }

    [TestMethod]
    public void SameFileSectionKeepsSeparateAssignmentResultsAcrossReload()
    {
        var database = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            var service = new CommentPersistenceService(database);
            service.SaveComments("Program.cs", new[] { "lab1", "lab2", "" }.Select(context => new SectionFeedback
            {
                ReviewContext = context, SectionName = "main", StartLine = 1, EndLine = 3,
                Explanation = context.Length == 0 ? "Legacy" : context
            }));
            var reloaded = new CommentPersistenceService(database).LoadComments("Program.cs");
            Assert.AreEqual(3, reloaded.Count);
            Assert.AreEqual("lab1", reloaded.Single(c => c.ReviewContext == "lab1").Explanation);
            Assert.AreEqual("lab2", reloaded.Single(c => c.ReviewContext == "lab2").Explanation);
            Assert.AreEqual("Legacy", reloaded.Single(c => c.ReviewContext.Length == 0).Explanation);
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(database); }
    }

    [TestMethod]
    public void RelatedContextDoesNotIncludeOtherLabsOrStudentsFromOpenTabs()
    {
        var root = Path.Combine(Path.GetTempPath(), "ReviewIsolation-" + Guid.NewGuid());
        try
        {
            var lab1 = Path.Combine(root, "StudentA", "Lab1");
            var lab2 = Path.Combine(root, "StudentA", "Lab2");
            var studentB = Path.Combine(root, "StudentB", "Lab1");
            foreach (var folder in new[] { lab1, lab2, studentB })
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "Types.h"), "struct Types {};");
            }
            var primary = Path.Combine(lab1, "Program.cpp");
            File.WriteAllText(primary, "#include \"Types.h\"\n#include \"../Lab2/Types.h\"\nint main() {}");
            var related = RelatedFileResolver.FindRelatedFiles(primary,
                [Path.Combine(lab2, "Types.h"), Path.Combine(studentB, "Types.h")], lab1);
            Assert.AreEqual(1, related.Count);
            Assert.AreEqual(Path.Combine(lab1, "Types.h"), related.Single().FilePath);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
