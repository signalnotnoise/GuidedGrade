using GuidedGrade.Models;
using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class StudentDataSanitizerTests
{
    [TestMethod]
    public void Sanitize_RedactsStudentNameAndId()
    {
        var student = new Student("Ada", "Lovelace", "12345", @"C:\labs\Lovelace_Ada-12345");
        var identifiers = StudentDataSanitizer.GetIdentifiers(student);
        var result = StudentDataSanitizer.Sanitize("Reviewed by Ada Lovelace, id 12345", identifiers);

        Assert.IsFalse(result.Contains("Ada", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(result.Contains("Lovelace", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(result.Contains("12345", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("[REDACTED]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Sanitize_RedactsEmailAndUserPath()
    {
        var result = StudentDataSanitizer.Sanitize(
            "Contact ada@example.com from C:\\Users\\alovelace\\lab.cpp");

        Assert.IsFalse(result.Contains("ada@example.com", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(result.Contains(@"C:\Users\alovelace", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(result, "[REDACTED_EMAIL]");
        StringAssert.Contains(result, @"C:\Users\[REDACTED]");
    }

    [TestMethod]
    public void Sanitize_LeavesAnonymousCodeIntact()
    {
        var code = "int Add(int left, int right)\n{\n    return left + right;\n}";
        var result = StudentDataSanitizer.Sanitize(code);

        Assert.AreEqual(code, result);
    }

    [TestMethod]
    public void AnonymousFileName_DoesNotIncludeStudentPath()
    {
        var name = StudentDataSanitizer.AnonymousFileName(2, ".h");
        Assert.AreEqual("section-2.h", name);
    }

    [TestMethod]
    public void SafeDisplayName_KeepsOriginalHeaderName()
    {
        var student = new Student("Ada", "Lovelace", "12345", @"C:\labs\Lovelace_Ada-12345");
        var identifiers = StudentDataSanitizer.GetIdentifiers(student);
        var name = StudentDataSanitizer.SafeDisplayName("Item.h", identifiers, 2);

        Assert.AreEqual("Item.h", name);
    }

    [TestMethod]
    public void SafeDisplayName_RedactsStudentNameInFileName()
    {
        var student = new Student("Ada", "Lovelace", "12345", @"C:\labs\Lovelace_Ada-12345");
        var identifiers = StudentDataSanitizer.GetIdentifiers(student);
        var name = StudentDataSanitizer.SafeDisplayName("Lovelace_lab.cpp", identifiers, 3);

        Assert.AreEqual("section-3.cpp", name);
    }
}
