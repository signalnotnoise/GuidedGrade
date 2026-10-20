using GuidedGrade.Services;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class GradePersistenceTests
{
    [TestMethod]
    public void RunningTotalUsesPointsAcrossAssignmentsAndExcludesOtherStudentsAndCourses()
    {
        var folder = Path.Combine(Path.GetTempPath(), "StudentA");
        string Key(string student, string course, string assignment) => ReviewContext.Key(student,
            new GuidedGrade.Models.GradingAssignment { Course = course, Title = assignment }, Path.Combine(student, "Lab", "Program.cs"));
        var grades = new Dictionary<string, StudentGrade>
        {
            [Key(folder, "Course1", "First")] = new(45, 50),
            [Key(folder, "Course1", "Second")] = new(70, 100),
            [Key(folder, "Course2", "First")] = new(0, 100),
            [Key(folder + "B", "Course1", "First")] = new(0, 100)
        };
        Assert.AreEqual(new StudentGrade(115, 150), GradeTotals.ForCourse(grades, folder, "Course1"));
        grades.Remove(Key(folder, "Course1", "Second"));
        Assert.AreEqual(new StudentGrade(45, 50), GradeTotals.ForCourse(grades, folder, "Course1"));
        Assert.IsNull(GradeTotals.ForCourse(grades, folder, "Ungraded course"));
    }

    [TestMethod]
    public void GradesSurviveReloadAndClearOnlyTheirOwnContext()
    {
        var database = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            var service = new GradePersistenceService(database);
            service.Save("A/Lab1", new(42, 50));
            service.Save("A/Lab2", new(0, 100));
            service.Save("B/Lab1", new(9, 10));
            service.Save("A/Lab1", new(45, 50));
            var reloaded = new GradePersistenceService(database).LoadAll();
            Assert.AreEqual(90, reloaded["A/Lab1"].Percentage);
            Assert.AreEqual(0, reloaded["A/Lab2"].Earned);
            service.Save("A/Lab1", null);
            Assert.AreEqual(2, service.LoadAll().Count);
            Assert.IsFalse(service.LoadAll().ContainsKey("A/Lab1"));
            foreach (var grade in new[] { new StudentGrade(double.NaN, 100), new(-1, 100), new(5, 0), new(101, 100), new(1, double.PositiveInfinity) })
                Assert.ThrowsException<ArgumentException>(() => service.Save("invalid", grade));
            Assert.AreEqual(2, service.LoadAll().Count);
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(database); }
    }
}
