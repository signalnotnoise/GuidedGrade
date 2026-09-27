using System.IO.Compression;
using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class ZipFileHandlerTests
{
    private string _root = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "LabFeedbackTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

    private string CreateArchive(params string[] names)
    {
        var path = Path.Combine(_root, "submission.zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var name in names)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write("student source");
        }
        return path;
    }

    [TestMethod]
    [DataRow("../outside.txt")]
    [DataRow("..\\outside.txt")]
    [DataRow("../output-sibling/outside.txt")]
    [DataRow("nested/../../outside.txt")]
    [DataRow("C:\\outside.txt")]
    [DataRow("/outside.txt")]
    [DataRow("\\\\server\\share\\outside.txt")]
    [DataRow("file.txt:stream")]
    [DataRow(".. /outside.txt")]
    public async Task Extract_RejectsUnsafeEntriesBeforeWriting(string entry)
    {
        var archive = CreateArchive("valid.cpp", entry);
        var output = Path.Combine(_root, "output");
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() =>
            ZipFileHandler.ExtractSingleZipFileAsync(archive, output, CancellationToken.None));

        Assert.IsFalse(Directory.Exists(output), "Preflight should reject the archive before extracting valid entries.");
        Assert.IsFalse(File.Exists(Path.Combine(_root, "outside.txt")));
        Assert.IsTrue(File.Exists(archive));
    }

    [TestMethod]
    public async Task Extract_WritesNestedFilesAndOverwritesExistingFiles()
    {
        var archive = CreateArchive("nested/main.cpp");
        var output = Path.Combine(_root, "output");
        Directory.CreateDirectory(Path.Combine(output, "nested"));
        var source = Path.Combine(output, "nested", "main.cpp");
        File.WriteAllText(source, "old source");

        await ZipFileHandler.ExtractSingleZipFileAsync(archive, output, CancellationToken.None);

        Assert.AreEqual("student source", File.ReadAllText(source));
    }

    [TestMethod]
    public async Task Extract_PropagatesWriteFailureAndRetainsArchive()
    {
        var archive = CreateArchive("main.cpp");
        var output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);
        var target = Path.Combine(output, "main.cpp");
        File.WriteAllText(target, "original");
        File.SetAttributes(target, FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() =>
                ZipFileHandler.ExtractSingleZipFileAsync(archive, output, CancellationToken.None));
            Assert.IsTrue(File.Exists(archive));
            Assert.AreEqual("original", File.ReadAllText(target));
        }
        finally { File.SetAttributes(target, FileAttributes.Normal); }
    }
}
