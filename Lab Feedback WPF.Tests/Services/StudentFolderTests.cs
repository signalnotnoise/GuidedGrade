using Lab_Feedback_WPF.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class StudentFolderTests
{
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
