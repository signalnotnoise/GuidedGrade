using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.Sqlite;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class DecimalFeedbackTests
{
    [TestMethod]
    public void DecimalScoreIsNotTruncatedAndParsingIsCultureIndependent()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new("de-DE");
            Assert.AreEqual(2.5, SectionGradingService.ParseFeedback("SCORE: 2.5/3", "Test").SuggestedScore);
            Assert.AreEqual(3.0, SectionGradingService.ParseFeedback("SCORE: 3/3", "Test").SuggestedScore);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    [TestMethod]
    public void ExistingIntegerAffinityColumnRetainsFractionalScores()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE Feedback (SuggestedScore INTEGER NOT NULL); INSERT INTO Feedback VALUES (3), (2.5); SELECT SuggestedScore FROM Feedback ORDER BY rowid;";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(3.0, reader.GetDouble(0));
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(2.5, reader.GetDouble(0));
    }
}
