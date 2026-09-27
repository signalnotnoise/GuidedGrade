using Lab_Feedback_WPF.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lab_Feedback_WPF_Tests.Services;

[TestClass]
public class RelatedFileResolverTests
{
    [TestMethod]
    public void FindRelatedFiles_IncludesHeaderAndQuotedInclude()
    {
        var root = CreateTempDir();
        try
        {
            var cppPath = Path.Combine(root, "Vector.cpp");
            var headerPath = Path.Combine(root, "Vector.h");
            var helperPath = Path.Combine(root, "VectorUtils.h");

            File.WriteAllText(headerPath, "struct Vector { int size; };");
            File.WriteAllText(helperPath, "int helper();");
            File.WriteAllText(cppPath, "#include \"Vector.h\"\n#include \"VectorUtils.h\"\nint ChangeVectorSize() { return 0; }");

            var related = RelatedFileResolver.FindRelatedFiles(cppPath);

            Assert.AreEqual(2, related.Count);
            Assert.IsTrue(related.Any(file => file.FileName == "Vector.h"));
            Assert.IsTrue(related.Any(file => file.FileName == "VectorUtils.h"));
            Assert.IsTrue(related.Any(file => file.Content.Contains("struct Vector")));
            Assert.IsTrue(related.Any(file => file.Content.Contains("int helper();")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void FindRelatedFiles_DoesNotIncludePrimaryFile()
    {
        var root = CreateTempDir();
        try
        {
            var cppPath = Path.Combine(root, "Vector.cpp");
            File.WriteAllText(cppPath, "int ChangeVectorSize() { return 0; }");

            var related = RelatedFileResolver.FindRelatedFiles(cppPath);

            Assert.AreEqual(0, related.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void FindRelatedFiles_IncludesNestedIncludeAndOpenTabs()
    {
        var root = CreateTempDir();
        try
        {
            var headers = Path.Combine(root, "Headers");
            Directory.CreateDirectory(headers);

            var cppPath = Path.Combine(root, "RPG_Shop.cpp");
            var inventoryPath = Path.Combine(headers, "Inventory.h");
            var itemPath = Path.Combine(headers, "Item.h");

            File.WriteAllText(itemPath, "class Item { public: int gold; };");
            File.WriteAllText(inventoryPath, "#include \"Item.h\"\nclass Inventory { Item items[5]; };");
            File.WriteAllText(cppPath, "#include \"Inventory.h\"\nint main() { return 0; }");

            var related = RelatedFileResolver.FindRelatedFiles(
                cppPath,
                additionalPaths: new[] { itemPath, inventoryPath },
                searchRoot: root);

            Assert.IsTrue(related.Any(file => file.FileName == "Item.h"));
            Assert.IsTrue(related.Any(file => file.FileName == "Inventory.h"));
            Assert.IsTrue(related.First(file => file.FileName == "Item.h").DeclaredTypes.Contains("Item"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void ExtractDeclaredTypes_IncludesClassNameFromHeader()
    {
        var types = RelatedFileResolver.ExtractDeclaredTypes("class Item {\npublic:\n    int gold;\n};\n", "Item");

        CollectionAssert.Contains(types.ToList(), "Item");
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "guided-grade-related-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
