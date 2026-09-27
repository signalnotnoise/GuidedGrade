using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class AdversarialInputPlannerTests
{
    [TestMethod]
    public void ParseOrDefault_ReadsJsonArray()
    {
        var cases = AdversarialInputPlanner.ParseOrDefault("""
            [
              {"name":"empty","stdin":"","rationale":"no input"},
              {"name":"bad","stdin":"abc\n","rationale":"invalid"}
            ]
            """);

        Assert.AreEqual(2, cases.Count);
        Assert.AreEqual("empty", cases[0].Name);
        Assert.AreEqual("bad", cases[1].Name);
        StringAssert.Contains(cases[1].Stdin, "abc");
    }

    [TestMethod]
    public void ParseOrDefault_ExtractsJsonFromMarkdown()
    {
        var cases = AdversarialInputPlanner.ParseOrDefault("""
            Here you go:
            ```json
            [{"name":"overflow","stdin":"9999999999\n","rationale":"too big"}]
            ```
            """);

        Assert.AreEqual(1, cases.Count);
        Assert.AreEqual("overflow", cases[0].Name);
    }

    [TestMethod]
    public void ParseOrDefault_FallsBackWhenInvalid()
    {
        var cases = AdversarialInputPlanner.ParseOrDefault("not json");
        Assert.IsTrue(cases.Count >= 4);
        Assert.IsTrue(cases.Any(c => c.Name == "empty"));
        Assert.IsTrue(cases.Any(c => c.Name == "non-numeric"));
    }
}
