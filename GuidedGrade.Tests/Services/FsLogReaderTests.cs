using System.Text;
using GuidedGrade.Services;
using GuidedGrade.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace GuidedGrade.Tests.Services;
[TestClass]
public class FsLogReaderTests
{
    [TestMethod]
    public void Pg2RulePresetDoesNotApplyToOtherCourses()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            var persistence = new AssignmentPersistenceService(path);
            var model = new GuidedGrade.ViewModels.ClassSettingsViewModel(persistence);
            model.Load("PG1"); model.AddCppRules(); Assert.AreEqual("", model.ReviewRules.Value);
            model.Load("PG2"); model.AddCppRules(); model.Save("PG2");
            StringAssert.Contains(persistence.LoadCourseReviewRules("PG2"), "No lambda");
            model.Load("DSA"); Assert.AreEqual("", model.ReviewRules.Value);
            persistence.SaveAssignment(new GradingAssignment { Course = "PG2", Title = "Lab 1" });
            var assignment = persistence.LoadAssignment("PG2", "Lab 1")!;
            StringAssert.Contains(AssignmentGradingInstructions.Build(assignment), "No lambda");
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [TestMethod]
    public void DecodesTimestampedSnapshotsAndRejectsTruncatedChunks()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(0x474C5346u); writer.Write(2f); writer.Write(2u);
                foreach (var name in new[] { "Game.cpp", "Game.h" }) { var bytes = Encoding.ASCII.GetBytes(name); writer.Write((uint)bytes.Length); writer.Write(bytes); }
                foreach (var time in new[] { 1700000000ul, 1700000060ul })
                {
                    writer.Write(time);
                    foreach (var text in new[] { "void Run() {}", "void Run();" }) { var bytes = Encoding.ASCII.GetBytes(text).Select(b => unchecked((byte)(b + 128))).ToArray(); writer.Write((uint)bytes.Length); writer.Write(bytes); }
                }
            }
            var log = FsLogReader.Read(path);
            Assert.AreEqual(4, log.Snapshots.Count);
            Assert.AreEqual(2, log.BuildCount);
            Assert.AreEqual("void Run() {}", log.Snapshots[0].Content);
            Assert.AreEqual("Game.h", log.Snapshots[1].Name);
            Assert.AreEqual(60d, (log.Snapshots[2].Time - log.Snapshots[0].Time).TotalSeconds);
            var data = File.ReadAllBytes(path); File.WriteAllBytes(path, data[..^1]);
            Assert.ThrowsException<System.IO.InvalidDataException>(() => FsLogReader.Read(path));
        }
        finally { File.Delete(path); }
    }
    [TestMethod]
    public void AssignmentPathsPersistAndSnapshotIndependently()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            var service = new AssignmentPersistenceService(path);
            var assignment = new GradingAssignment { Title = "Lab 1", ReviewFilePaths = ["**/Week1/Game.cpp", "**/Week1/Game.h"], LogFilePath = "**/Week1/Lab1.fslog" };
            assignment.FeedbackOptions.DetailLevel = 2; assignment.FeedbackOptions.ReadingLevel = "High school";
            service.SaveAssignment(assignment);
            var loaded = service.LoadAssignment("General", "Lab 1")!;
            CollectionAssert.AreEqual(assignment.ReviewFilePaths, loaded.ReviewFilePaths);
            Assert.AreEqual(assignment.LogFilePath, loaded.LogFilePath);
            Assert.AreEqual(2, loaded.FeedbackOptions.DetailLevel);
            Assert.AreEqual("High school", loaded.FeedbackOptions.ReadingLevel);
            StringAssert.Contains(AssignmentGradingInstructions.Build(loaded), "Brief: use one compact line");
            StringAssert.Contains(AssignmentGradingInstructions.Build(loaded), "high-school level");
            var snapshot = ReviewContext.Snapshot(loaded)!; loaded.ReviewFilePaths.Clear();
            Assert.AreEqual(2, snapshot.ReviewFilePaths.Count);
            assignment.LogFilePath = "../Other.fslog";
            Assert.ThrowsException<ArgumentException>(() => service.SaveAssignment(assignment));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }
}
