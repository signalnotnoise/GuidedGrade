using GuidedGrade.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class StudentFolderTests
{
    [TestMethod]
    public void FolderNameModeListsRepositoriesWithoutInventingStudentIds()
    {
        var root = Path.Combine(Path.GetTempPath(), "FolderMode-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "my-repository"));
            Directory.CreateDirectory(Path.Combine(root, "Example_Alex-123"));
            Directory.CreateDirectory(Path.Combine(root, "my-repository", "nested"));
            var folders = Student.GetStudentsFromFolders(root, useFolderNames: true);
            CollectionAssert.AreEquivalent(new[] { "my-repository", "Example_Alex-123" }, folders.Select(s => s.FullName).ToArray());
            Assert.IsTrue(folders.All(s => s.IdNumber == "" && Directory.Exists(s.Folder)));
            Assert.AreEqual(1, Student.GetStudentsFromFolders(root).Count);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void ClassFolderPreferenceDefaultsToStudentFormatAndPersistsIndependently()
    {
        var root = Path.Combine(Path.GetTempPath(), "FolderSettings-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var database = Path.Combine(root, "assignments.db");
            var service = new GuidedGrade.Services.AssignmentPersistenceService(database);
            Assert.IsFalse(service.UseFolderNames("PG1"));
            Assert.IsFalse(service.UseFolderNames("DSA"));
            service.SaveFolderNames("PG1", true);
            var reopened = new GuidedGrade.Services.AssignmentPersistenceService(database);
            Assert.IsTrue(reopened.UseFolderNames(" pg1 "));
            Assert.IsFalse(reopened.UseFolderNames("DSA"));
            reopened.SaveFolderNames("PG1", false);
            Assert.IsFalse(service.UseFolderNames("PG1"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("notes")]
    [DataRow("Example_Alex")]
    [DataRow("Example_-123")]
    [DataRow("_Alex-123")]
    [DataRow("Example_Alex-")]
    public void InvalidFolderHasExplicitValidationError(string? folder)
    {
        Assert.ThrowsException<ArgumentException>(() => new Student("root", folder));
    }

    [TestMethod]
    public void DiscoverySkipsInvalidFoldersAndPreservesValidStudents()
    {
        var root = Path.Combine(Path.GetTempPath(), "StudentFolderTests-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "invalid"));
            var valid = Path.Combine(root, "Example_Alex-123");
            Directory.CreateDirectory(valid);
            var students = Student.GetStudentsFromFolders(root);
            Assert.AreEqual(1, students.Count);
            Assert.AreEqual("Example", students[0].LastName);
            Assert.AreEqual("Alex", students[0].FirstName);
            Assert.AreEqual("123", students[0].IdNumber);
            Assert.AreEqual(valid, students[0].Folder);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
