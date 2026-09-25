using Lab_Feedback_WPF.Models;
using Lab_Feedback_WPF.Services;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class ClearReviewTests
{
    [TestMethod]
    public void LegacyCommentsGainOverallFlagWithoutLosingExistingReview()
    {
        var database = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            using (var connection = new SqliteConnection($"Data Source={database}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE SectionComments (
                        Id INTEGER PRIMARY KEY, FilePath TEXT NOT NULL, SectionName TEXT NOT NULL,
                        StartLine INTEGER NOT NULL, EndLine INTEGER NOT NULL, SuggestedScore REAL NOT NULL,
                        Strengths TEXT NOT NULL, Issues TEXT NOT NULL, SuggestedCode TEXT NOT NULL,
                        Explanation TEXT NOT NULL);
                    INSERT INTO SectionComments VALUES (1, 'One.cs', 'Legacy section', 2, 4, 2.5, '[]', '[]', '', 'Existing review');
                    """;
                command.ExecuteNonQuery();
            }
            var service = new CommentPersistenceService(database);
            var comments = service.LoadComments("One.cs");
            Assert.AreEqual(1, comments.Count);
            Assert.IsFalse(comments[0].IsOverallReview);
            Assert.AreEqual(2.5, comments[0].SuggestedScore);
            comments.Add(new SectionFeedback { IsOverallReview = true, SectionName = "Overall file review", StartLine = 1, EndLine = 1, Explanation = "Full report" });
            service.SaveComments("One.cs", comments);
            var reloaded = new CommentPersistenceService(database).LoadComments("One.cs");
            Assert.AreEqual(2, reloaded.Count);
            Assert.AreEqual("Full report", reloaded.Single(comment => comment.IsOverallReview).Explanation);
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(database); }
    }

    [TestMethod]
    public void ClearDeletesEveryReviewStatusForOnlyTheTargetFileAndSurvivesReload()
    {
        var database = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            var service = new CommentPersistenceService(database);
            service.SaveComments("C:\\Work\\One.cpp", Enum.GetValues<FeedbackReviewStatus>().Select((status, index) =>
                new SectionFeedback { SectionName = status.ToString(), StartLine = index + 1, EndLine = index + 1, ReviewStatus = status }));
            service.SaveComments("C:\\Work\\Two.cpp", new[] { new SectionFeedback { SectionName = "Keep", StartLine = 1, EndLine = 2 } });
            service.DeleteComments("c:\\work\\one.cpp");
            var reloaded = new CommentPersistenceService(database);
            Assert.AreEqual(0, reloaded.LoadComments("C:\\Work\\One.cpp").Count);
            Assert.AreEqual(1, reloaded.LoadComments("C:\\Work\\Two.cpp").Count);
            reloaded.DeleteComments("C:\\Work\\One.cpp");
            Assert.AreEqual(1, reloaded.LoadComments("C:\\Work\\Two.cpp").Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(database);
        }
    }

    [TestMethod]
    public void ClearInvalidatesOldWorkForOnlyThatFileAndAllowsNewReviews()
    {
        var reviews = new ReviewGeneration();
        var oldVersion = reviews.Capture("One.cpp");
        var otherVersion = reviews.Capture("Two.cpp");
        reviews.Clear("ONE.cpp");
        Assert.IsFalse(reviews.IsCurrent("One.cpp", oldVersion));
        Assert.IsTrue(reviews.IsCurrent("Two.cpp", otherVersion));
        var newVersion = reviews.Capture("One.cpp");
        Assert.IsTrue(reviews.IsCurrent("One.cpp", newVersion));
        reviews.Clear("One.cpp");
        Assert.IsFalse(reviews.IsCurrent("One.cpp", newVersion));
    }
}
